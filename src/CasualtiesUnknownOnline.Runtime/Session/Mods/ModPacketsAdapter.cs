using System;
using System.Collections.Generic;
using CasualtiesUnknownOnline.Abstractions;
using Microsoft.Extensions.Logging;

namespace CasualtiesUnknownOnline.Runtime.Session.Mods;

/// <summary>
/// One mod's declared-packet registry, send surface and chain runner. It is
/// scoped by construction to one mod id (the adapter belongs to exactly one
/// <see cref="ModContext"/>), so a mod can neither declare nor send under
/// another mod's identity, and the id that rides the wire is always its own.
///
/// The adapter owns the mod-authored half of the mechanism — the declaration
/// table and the chain — while <see cref="ModChannel"/> owns the frame and
/// <see cref="ModLifecycle"/> owns routing and the relay. Every refusal is a
/// log line and a false return, and every handler runs inside its own
/// try/catch: a packet a mod declared badly, a payload it cannot parse or a
/// handler that throws can never wedge the receive path.
/// </summary>
internal sealed class ModPacketsAdapter(
	ModChannel channel,
	ModManifest manifest,
	ISessionControl session,
	ILogger log) : IModPackets
{
	private static readonly ModPacketStage[] StageOrder =
		[ModPacketStage.Validate, ModPacketStage.Apply, ModPacketStage.Observe];

	private readonly Dictionary<string, DeclaredPacket> _packets = [with(StringComparer.Ordinal)];
	private readonly List<string> _order = [];
	private readonly HashSet<string> _runningPackets = [with(StringComparer.Ordinal)];

	public bool CanSend => ModPermissionGate.HasPermission(manifest, ModPermission.SendNetworkMessage);

	public IReadOnlyCollection<string> PacketIds => [.. _order];

	public bool Register(ModPacket packet)
	{
		if (!RequirePermission())
		{
			return false;
		}

		if (packet is null || !ModPacketPolicy.IsValidId(packet.Id))
		{
			log.LogWarning("[Mods] {ModId} tried to declare a packet with an invalid id '{PacketId}' — refused.",
				manifest.Id, packet?.Id);
			return false;
		}

		if (!ModPacketPolicy.IsValidChain(packet))
		{
			log.LogWarning("[Mods] {ModId}/{PacketId} declared an empty, null-bearing or over-{Cap}-handler chain — refused.",
				manifest.Id, packet.Id, ModPacketPolicy.MaxHandlersPerPacket);
			return false;
		}

		if (_packets.ContainsKey(packet.Id))
		{
			log.LogWarning("[Mods] {ModId}/{PacketId} is already declared — the duplicate is refused.",
				manifest.Id, packet.Id);
			return false;
		}

		if (!ModPacketPolicy.CanAdd(_packets.Count))
		{
			log.LogWarning("[Mods] {ModId} reached the {Cap}-packet declaration cap — {PacketId} refused.",
				manifest.Id, ModPacketPolicy.MaxPacketsPerMod, packet.Id);
			return false;
		}

		_packets[packet.Id] = new DeclaredPacket(packet, OrderChain(packet));
		_order.Add(packet.Id);
		log.LogInformation("[Mods] {ModId} declared packet {PacketId} ({Sender}, {Delivery}, {Handlers} handler(s)).",
			manifest.Id, packet.Id, packet.Sender, packet.Delivery, packet.Handlers.Count);
		return true;
	}

	public bool Unregister(string packetId)
	{
		if (packetId is null || !_packets.Remove(packetId))
		{
			log.LogWarning("[Mods] {ModId} tried to retire undeclared packet {PacketId} — refused.", manifest.Id, packetId);
			return false;
		}

		_order.Remove(packetId);
		log.LogInformation("[Mods] {ModId} retired packet {PacketId}.", manifest.Id, packetId);
		return true;
	}

	public bool IsRegistered(string packetId) => packetId is not null && _packets.ContainsKey(packetId);

	public bool SendToHost(string packetId, byte[] payload)
	{
		if (!TryBeginSend(packetId, payload, out var declared) || !CheckLocalSender(declared.Packet))
		{
			return false;
		}

		if (session.Role != SessionRole.Host)
		{
			// A guest's report: the host's copy is the only copy it can reach.
			// Only EveryMember runs the reporter's own copy, and a local refusal
			// stops the frame before it leaves.
			if (declared.Packet.Delivery == ModPacketDelivery.EveryMember && !RunLocal(declared, payload))
			{
				return false;
			}

			return channel.SendPacketToHost(manifest.Id, declared.Packet.Id, payload);
		}

		if (declared.Packet.Delivery == ModPacketDelivery.EveryOtherMember)
		{
			log.LogWarning("[Mods] {ModId}/{PacketId} is declared for every member except its sender, and the host is the sender with no other copy to reach — the send is refused.",
				manifest.Id, declared.Packet.Id);
			return false;
		}

		// The host reaches its own copy without a wire hop.
		return RunLocal(declared, payload);
	}

	public bool SendToPeer(ulong steamId, string packetId, byte[] payload)
	{
		if (!TryBeginSend(packetId, payload, out var declared)
			|| !CheckLocalSender(declared.Packet)
			|| !CheckHostRoute(declared.Packet))
		{
			return false;
		}

		if (declared.Packet.Delivery == ModPacketDelivery.HostOnly)
		{
			log.LogWarning("[Mods] {ModId}/{PacketId} is declared host-only — no member copy may run it, so the directed send is refused.",
				manifest.Id, declared.Packet.Id);
			return false;
		}

		if (!session.TryGetMember(steamId, out var member) || !member.Handshaken)
		{
			log.LogWarning("[Mods] {ModId}/{PacketId} directed send to {Peer} refused: not a handshaken member of this session.",
				manifest.Id, declared.Packet.Id, steamId);
			return false;
		}

		if (declared.Packet.Delivery == ModPacketDelivery.EveryMember && !RunLocal(declared, payload))
		{
			return false;
		}

		return channel.SendPacketToPeer(manifest.Id, steamId, declared.Packet.Id, payload);
	}

	public bool Broadcast(string packetId, byte[] payload)
	{
		if (!TryBeginSend(packetId, payload, out var declared)
			|| !CheckLocalSender(declared.Packet)
			|| !CheckHostRoute(declared.Packet))
		{
			return false;
		}

		if (declared.Packet.Delivery == ModPacketDelivery.HostOnly)
		{
			log.LogWarning("[Mods] {ModId}/{PacketId} is declared host-only — no member copy may run it, so the broadcast is refused.",
				manifest.Id, declared.Packet.Id);
			return false;
		}

		// EveryOtherMember excludes the sender, which here is the host itself,
		// so the host's own copy does not run; EveryMember runs it first.
		if (declared.Packet.Delivery == ModPacketDelivery.EveryMember && !RunLocal(declared, payload))
		{
			return false;
		}

		return channel.SendPacketToAll(manifest.Id, declared.Packet.Id, payload);
	}

	/// <summary>
	/// Route one received declared-packet frame on this copy: look the
	/// declaration up, judge the sender against it, run the chain and answer
	/// whether the host still owes the other members a relay.
	/// </summary>
	internal ModPacketRoute Route(ulong sender, string packetId, byte[] payload)
	{
		if (packetId is null || !_packets.TryGetValue(packetId, out var declared))
		{
			log.LogWarning("[Mods] message for {ModId}/{PacketId} from {Sender} — no declared packet with that id on this copy, dropped.",
				manifest.Id, packetId, sender);
			return ModPacketRoute.UnknownPacket;
		}

		// The sender policy is judged where the sender's role is still known.
		// On a guest every frame comes from the host, and the host already
		// judged the report before relaying it, so a guest never re-judges it.
		if (session.Role == SessionRole.Host && !IsSenderRoleAllowed(declared.Packet, sender))
		{
			log.LogWarning("[Mods] {ModId}/{PacketId} from {Sender} refused: the packet is declared {Policy} and this sender may not start it.",
				manifest.Id, declared.Packet.Id, sender, declared.Packet.Sender);
			return ModPacketRoute.Refused;
		}

		// The chain works on its own copy of the payload whenever this delivery
		// may be relayed: the frame the host sends on carries the bytes it
		// received, whatever a handler made of its copy (the interface promises
		// exactly that, and a mod that rewrites its buffer before relaying must
		// not silently rewrite the wire).
		var chainPayload = session.Role == SessionRole.Host && declared.Packet.Delivery != ModPacketDelivery.HostOnly
			? (byte[])payload.Clone()
			: payload;
		var context = new PacketContext(declared.Packet.Id, sender, chainPayload, session.Role == SessionRole.Host, log);
		if (!RunGuarded(declared, context))
		{
			return ModPacketRoute.Refused;
		}

		if (context.Refusal is not null)
		{
			log.LogWarning("[Mods] {ModId}/{PacketId} from {Sender} refused by the validate stage: {Reason}",
				manifest.Id, declared.Packet.Id, sender, context.Refusal);
			return ModPacketRoute.Refused;
		}

		return session.Role == SessionRole.Host && declared.Packet.Delivery != ModPacketDelivery.HostOnly
			? ModPacketRoute.Relay
			: ModPacketRoute.Applied;
	}

	// ---- Internals ----

	private bool TryBeginSend(string packetId, byte[] payload, out DeclaredPacket declared)
	{
		declared = null!;
		if (!RequirePermission())
		{
			return false;
		}

		if (!ModPacketPolicy.IsValidPayload(payload))
		{
			log.LogWarning("[Mods] {ModId} tried to send packet {PacketId} with a null or over-{Cap}-byte payload — refused.",
				manifest.Id, packetId, ModChannel.MaxPayloadBytes);
			return false;
		}

		if (!session.SessionActive)
		{
			log.LogWarning("[Mods] {ModId} tried to send packet {PacketId} outside an active session — refused.",
				manifest.Id, packetId);
			return false;
		}

		if (packetId is null || !_packets.TryGetValue(packetId, out var found))
		{
			log.LogWarning("[Mods] {ModId} tried to send undeclared packet {PacketId} — refused.", manifest.Id, packetId);
			return false;
		}

		declared = found;
		return true;
	}

	private bool RequirePermission()
	{
		if (ModPermissionGate.HasPermission(manifest, ModPermission.SendNetworkMessage))
		{
			return true;
		}

		log.LogWarning("[Mods] {ModId} does not declare {Permission} — the packet call is refused.",
			manifest.Id, "SendNetworkMessage");
		return false;
	}

	private bool CheckLocalSender(ModPacket packet)
	{
		if (IsSenderRoleAllowed(packet, session.LocalSteamId))
		{
			return true;
		}

		log.LogWarning("[Mods] {ModId}/{PacketId} is declared {Policy}-only and this copy is the {Role} — the send is refused.",
			manifest.Id, packet.Id, packet.Sender, session.Role);
		return false;
	}

	private bool CheckHostRoute(ModPacket packet)
	{
		if (session.Role == SessionRole.Host)
		{
			return true;
		}

		log.LogWarning("[Mods] {ModId}/{PacketId} cannot be sent to a member or broadcast from a guest — the star has no member-to-member channel. The send is refused.",
			manifest.Id, packet.Id);
		return false;
	}

	/// <summary>The role a sender id stands for against the declaration: the host may start a host-only packet, anyone else a guest-only one.</summary>
	private bool IsSenderRoleAllowed(ModPacket packet, ulong sender) => packet.Sender switch
	{
		ModPacketSender.HostOnly => sender == session.HostSteamId,
		ModPacketSender.GuestOnly => sender != session.HostSteamId,
		_ => true,
	};

	/// <summary>Run this copy's chain for a locally-originated frame; false when it refused or is already running.</summary>
	private bool RunLocal(DeclaredPacket declared, byte[] payload)
	{
		// The local run gets its own copy for the same reason the host's relay
		// does: what a handler writes stays inside this delivery, and the frame
		// the send call carries keeps the bytes the caller passed.
		var context = new PacketContext(declared.Packet.Id, session.LocalSteamId, (byte[])payload.Clone(), session.Role == SessionRole.Host, log);
		if (!RunGuarded(declared, context))
		{
			return false;
		}

		if (context.Refusal is not null)
		{
			log.LogWarning("[Mods] {ModId}/{PacketId} local delivery refused by the validate stage: {Reason}",
				manifest.Id, declared.Packet.Id, context.Refusal);
			return false;
		}

		return true;
	}

	/// <summary>
	/// Run one delivery's chain behind the re-entrancy guard. A chain that sends
	/// (or relays) its own packet runs itself again inside its own run and would
	/// recurse until the stack ends the process — the one failure a mod can cause
	/// that the receive path's rate limit cannot bound, because a local run never
	/// crosses the wire. The second entry is refused with a log instead.
	/// </summary>
	private bool RunGuarded(DeclaredPacket declared, PacketContext context)
	{
		if (!_runningPackets.Add(declared.Packet.Id))
		{
			log.LogWarning("[Mods] {ModId}/{PacketId} delivery refused: this copy is already running that packet, and a chain that starts it again would recurse without bound.",
				manifest.Id, declared.Packet.Id);
			return false;
		}

		try
		{
			RunChain(declared, context);
			return true;
		}
		finally
		{
			_runningPackets.Remove(declared.Packet.Id);
		}
	}

	private void RunChain(DeclaredPacket declared, PacketContext context)
	{
		foreach (var handler in declared.Chain)
		{
			context.Stage = handler.Stage;
			try
			{
				handler.Handler(context);
			}
			catch (Exception e)
			{
				log.LogError(e, "[Mods] {ModId}/{PacketId} {Stage} handler threw — isolated, the chain continues.",
					manifest.Id, declared.Packet.Id, handler.Stage);
			}

			if (context.Refusal is not null)
			{
				return; // the first refusal settles the delivery
			}
		}
	}

	/// <summary>The stages in run order, the declared order kept inside each stage — computed once, at registration.</summary>
	private static ModPacketHandler[] OrderChain(ModPacket packet)
	{
		var ordered = new List<ModPacketHandler>(packet.Handlers.Count);
		foreach (var stage in StageOrder)
		{
			foreach (var handler in packet.Handlers)
			{
				if (handler.Stage == stage)
				{
					ordered.Add(handler);
				}
			}
		}

		return [.. ordered];
	}

	/// <summary>One registered declaration plus its chain in run order.</summary>
	private sealed record DeclaredPacket(ModPacket Packet, IReadOnlyList<ModPacketHandler> Chain);

	/// <summary>One delivery's context — the value every handler of that delivery shares.</summary>
	private sealed class PacketContext(string packetId, ulong sender, byte[] payload, bool isHost, ILogger log) : IModPacketContext
	{
		public string PacketId { get; } = packetId;

		public ModPacketStage Stage { get; set; }

		public ulong SenderSteamId { get; } = sender;

		public bool IsHost { get; } = isHost;

		public byte[] Payload { get; } = payload;

		internal string? Refusal { get; private set; }

		public void Refuse(string reason)
		{
			if (Stage != ModPacketStage.Validate)
			{
				log.LogWarning("[Mods] {PacketId} refused during {Stage} — the packet is already applied, so the refusal is ignored.",
					PacketId, Stage);
				return;
			}

			Refusal ??= string.IsNullOrWhiteSpace(reason) ? "(no reason given)" : reason;
		}
	}
}
