using System;
using System.Collections.Generic;
using System.Linq;
using CasualtiesUnknownOnline.Abstractions;
using CasualtiesUnknownOnline.Runtime.Protocol;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using CasualtiesUnknownOnline.Runtime.Session.Mods;
using Microsoft.Extensions.Logging;

namespace CasualtiesUnknownOnline.Runtime.Session.Handlers;

/// <summary>Guest → host: protocol negotiation + member creation (new join or reconnect).</summary>
[PacketHandler(NetMsg.Handshake)]
public sealed class HandshakeHandler(PacketSender sender, ILogger<HandshakeHandler> log) : PacketHandlerBase<HandshakeMsg>
{
	private readonly PacketSender _sender = sender;
	private readonly ILogger<HandshakeHandler> _log = log;

	protected override void Handle(ulong sender, HandshakeMsg msg, HandlerContext ctx)
	{
		var session = ctx.Session;
		if (session.Role != SessionRole.Host)
		{
			return;
		}

		var protocol = msg.Protocol;
		var peerState = (SceneStateType)msg.Scene.State;
		if (protocol != ProtocolVersion.Current)
		{
			_log.LogWarning("Peer {Peer} speaks protocol {PeerProtocol}; we speak {Current}. Rejecting.",
				sender, protocol, ProtocolVersion.Current);
			return;
		}

		// Star network: only lobby members may join — the lobby is the roster.
		// A third player is no longer rejected, they become a new member.
		if (!session.IsLobbyMember(sender))
		{
			_log.LogWarning("Handshake from {Peer} ignored: not a lobby member.", sender);
			return;
		}

		// Mod-list consistency (Phase 4 Mod API): the host validates the
		// member's declared mods against its own BEFORE the member is created —
		// a rejected member never enters the roster. Two deliberate windows:
		// discovery pending (the first frame has not run yet — the guest's 1 s
		// retry is checked then; production handshakes take seconds anyway, the
		// lazy Steam P2P session, so this window is practically unreachable)
		// and an old client's null list (treated as empty — but cross-version
		// sessions are refused by the protocol gate above anyway).
		if (!CheckModConsistency(sender, msg, ctx))
		{
			return;
		}

		var wasActive = session.SessionActive;
		if (!session.TryGetMember(sender, out var member))
		{
			member = session.GetOrCreateMember(sender);
			member.InWorld = peerState == SceneStateType.InWorld;
			// Cross-session restore: the disk-backed character save outlives the
			// session — a returning player gets it back after a host restart /
			// continue-run; a NEW run clears it at the host's start click.
			ctx.CharacterData.SendSavedCharacter(sender);
		}
		else
		{
			// Reconnect from the same player while the member is still held
			// (within the presence-check window, or a quick lobby round trip):
			// identity is the SteamID — reuse the presence. The normal flow
			// (session re-activation → scene re-report → entity sync) then
			// re-establishes everything, character data included.
			// A repeat handshake from a member we already CONFIRMED means the
			// process restarted (reconnect); from an unconfirmed one it means
			// the ack never reached it — the guest is retrying, and the start
			// gate must NOT wait on it (Handshaken stays false until the
			// end-to-end AckAck arrives, HandshakeAckAckHandler).
			if (member.Handshaken)
			{
				_log.LogInformation("Peer {Peer} reconnected — presence reused.", sender);
			}
			else
			{
				_log.LogWarning("Peer {Peer} is retrying its handshake — the previous ack was not delivered.", sender);
			}

			member.InWorld = peerState == SceneStateType.InWorld;
			ctx.CharacterData.SendSavedCharacter(sender);
		}

		// A member (re)entering while already InWorld never fires the
		// SceneStateHandler InWorld edge — its scene state was restored here
		// from the handshake report — so the world snapshots fan out here too.
		// Without this a reconnect got the character save but a stale world
		// (observed live: the spent spike not shown, the shuttle door closed
		// again, the trashbag contents regressed).
		if (member.InWorld)
		{
			ctx.SendWorldStateToMember(sender);
			// The Game Adapter's world-entry state (geyser liquid types, keypad
			// codes) lives in the Unity scene, so it cannot ride the Runtime
			// snapshot group — tell it the member (re)entered so it re-fans-out
			// immediately instead of waiting up to 60 s for its periodic cycle.
			ctx.Session.FireRemoteSceneChanged(sender, true);
		}

		// NOT Handshaken yet: the member only counts as handshaken once its
		// end-to-end AckAck arrives (HandshakeAckAckHandler) — a lost ack (lazy
		// Steam P2P session, cert errors) otherwise keeps a guest retrying while
		// the host treats it as connected and waits for it at the start gate.
		if (!wasActive)
		{
			// Fire the session-level event once, on the first member — later
			// members only take the member-level path.
			session.SessionActive = true;
			_log.LogInformation("Handshake complete with {Peer}.", sender);
			session.FireSessionActivated();
		}

		ctx.Entities.MaybeStartEntitySync();

		// Ack on every handshake, even repeats: the guest retransmits its
		// handshake until it receives one (Steam P2P sessions establish lazily,
		// first messages can be swallowed — Phase-0 finding). Same for world
		// params, which are only sent once the session exists.
		_sender.Send(sender, NetMsg.HandshakeAck, new HandshakeAckMsg
		{
			Protocol = ProtocolVersion.Current,
			Scene = new SceneStateMsg { State = (byte)session.LocalSceneState },
			HasWorldParams = ctx.World.WorldParams is not null,
		});
		var worldParams = ctx.World.WorldParams;
		if (worldParams is not null)
		{
			// Params go whenever they exist: a member joining mid-generation
			// needs them the moment the host's world-entry re-invite arrives.
			_sender.Send(sender, NetMsg.WorldStartParams, worldParams.ToWorldStartParamsMsg());
			// The explicit enter instruction when the host is in a world right
			// now, OR mid-generation (HostRunPending — the click-moment invite
			// went out before this member handshook; following immediately
			// starts its loading in parallel with the host's instead of a whole
			// generation late). Host in the menu (it captured the params for an
			// earlier run): no join — the guest would enter a world whose host
			// is not there and wait for a start gate that never arms. Order
			// matters: params first, then the join (the guest's run-start gate
			// passes once the params are in hand; the host owns the timing).
			if (session.LocalSceneState == SceneStateType.InWorld || ctx.World.HostRunPending)
			{
				_sender.Send(sender, NetMsg.WorldJoin, new WorldJoinMsg { IsTutorial = worldParams.IsTutorial });
			}
		}
	}

	/// <summary>
	/// The Phase 4 Mod API consistency check: the host's discovered mods vs the
	/// member's declared list. Policy (mirroring architecture.md §6):
	/// RequiresAllPlayers/Synchronized/Authoritative missing on either side, or
	/// version-unequal, → reject (the host cannot arbitrate state the member
	/// lacks or claims with a different version); HostOnly is host-side only (a
	/// guest lacking it passes); ClientOnly/Cosmetic differences pass (local
	/// surfaces). A malformed guest list (empty/duplicated id, Unspecified or unknown
	/// NetworkMode, invalid permissions, unparseable state-bearing SemVer) is
	/// rejected. Discovery pending → "not checked yet"
	/// refusal (the guest's 1 s handshake retry is then checked properly).
	/// </summary>
	private bool CheckModConsistency(ulong sender, HandshakeMsg msg, HandlerContext ctx)
	{
		var mods = ctx.Mods;
		if (!mods.IsDiscoveryComplete)
		{
			_log.LogWarning("Handshake from {Peer} ignored: mod discovery pending (the first frame has not run yet) — the retry is checked.", sender);
			return false;
		}

		var host = mods.CurrentModManifests;
		var guest = msg.Mods ?? []; // null = an old client's missing field — treated as an empty list

		// Shape: the member's list must be well-formed (empty/duplicated id,
		// Unspecified or unknown NetworkMode are all garbage in).
		var guestIds = new HashSet<string>(StringComparer.Ordinal);
		foreach (var info in guest)
		{
			if (string.IsNullOrWhiteSpace(info.Id) || !guestIds.Add(info.Id))
			{
				_log.LogWarning("Handshake from {Peer} rejected: malformed mod list (empty or duplicated id).", sender);
				return false;
			}

			if (info.NetworkMode == NetworkMode.Unspecified || !Enum.IsDefined(typeof(NetworkMode), info.NetworkMode))
			{
				_log.LogWarning("Handshake from {Peer} rejected: mod {Id} declares {Mode} — not a valid NetworkMode.", sender, info.Id, (int)info.NetworkMode);
				return false;
			}

			if (!ModPermissionPolicy.IsValidFor(info.NetworkMode, info.Permissions))
			{
				_log.LogWarning("Handshake from {Peer} rejected: mod {Id} declares invalid permissions {Permissions} for {Mode}.",
					sender, info.Id, info.Permissions, info.NetworkMode);
				return false;
			}
		}

		// Host has, member lacks or version-unequal: the state-bearing modes
		// reject; local-surface modes pass (HostOnly is host-side only).
		foreach (var hostMod in host)
		{
			var guestInfo = guest.FirstOrDefault(g => g.Id == hostMod.Id);
			if (guestInfo is null)
			{
				if (IsStateBearing(hostMod.NetworkMode))
				{
					_log.LogWarning("Handshake from {Peer} rejected: {Id} ({Mode}) is required and missing on the member.", sender, hostMod.Id, hostMod.NetworkMode);
					return false;
				}

				continue;
			}

			var hostStateBearing = IsStateBearing(hostMod.NetworkMode);
			var guestStateBearing = IsStateBearing(guestInfo.NetworkMode);
			if ((hostStateBearing || guestStateBearing) && guestInfo.NetworkMode != hostMod.NetworkMode)
			{
				_log.LogWarning("Handshake from {Peer} rejected: {Id} declares {Member} but the host declares {Host} — the network contract must match.",
					sender, hostMod.Id, guestInfo.NetworkMode, hostMod.NetworkMode);
				return false;
			}

			if (hostStateBearing)
			{
				if (!SemanticVersion.TryParse(guestInfo.Version, out var guestVersion)
					|| !SemanticVersion.TryParse(hostMod.Version, out var hostVersion)
					|| !guestVersion!.PrecedenceEquals(hostVersion!))
				{
					_log.LogWarning("Handshake from {Peer} rejected: {Id} version {Member} ≠ host {Host} by SemVer precedence ({Mode}).",
						sender, hostMod.Id, guestInfo.Version, hostMod.Version, hostMod.NetworkMode);
					return false;
				}

				if (guestInfo.Permissions != hostMod.Permissions)
				{
					_log.LogWarning("Handshake from {Peer} rejected: {Id} permissions {Member} ≠ host {Host} ({Mode}).",
						sender, hostMod.Id, guestInfo.Permissions, hostMod.Permissions, hostMod.NetworkMode);
					return false;
				}
			}
		}

		// Member claims a state-bearing mod the host does not run — the host
		// cannot arbitrate it, so it cannot be admitted.
		foreach (var guestInfo in guest)
		{
			if (host.All(h => h.Id != guestInfo.Id)
				&& guestInfo.NetworkMode is NetworkMode.RequiresAllPlayers or NetworkMode.Synchronized or NetworkMode.Authoritative)
			{
				_log.LogWarning("Handshake from {Peer} rejected: {Id} claims {Mode} but the host does not run it — the host cannot arbitrate it.",
					sender, guestInfo.Id, guestInfo.NetworkMode);
				return false;
			}
		}

		return true;
	}

	private static bool IsStateBearing(NetworkMode mode) =>
		mode is NetworkMode.RequiresAllPlayers or NetworkMode.Synchronized or NetworkMode.Authoritative;
}
