using System.Collections.Generic;

namespace CasualtiesUnknownOnline.Abstractions;

/// <summary>
/// The declared-packet surface of <see cref="IModContext"/> — the network
/// mechanism a mod with behaviour of its own builds its messages on, instead
/// of tunnelling every message through one opaque callback.
///
/// A mod declares a packet once: an id it owns, who may send it, which copies
/// run its chain and the chain itself (<see cref="ModPacket"/>). CUO owns
/// everything else — the frame, the routing, the relay and the refusal story —
/// so a mod never writes the report-and-fan-out dance by hand, and a second
/// mod's packets are the framework's mechanism rather than the first mod's
/// private shape.
///
/// The guarantees a declared packet comes with:
/// <list type="bullet">
/// <item>the frame rides the ordinary mod-message channel — reliable at the
/// transport, star topology, the same per-sender rate limit and the same
/// 64 KiB payload policy rail as <see cref="IModNetwork"/>;</item>
/// <item>the packet id travels beside the mod id, so a receiver routes by an
/// identity the mod owns rather than by a convention inside its payload;</item>
/// <item>the declared policy is enforced at both ends (a wrong-role send is
/// refused before it leaves, and the host judges a member's frame again);</item>
/// <item>a chain handler that throws is isolated and logged with the mod id,
/// the packet id and the stage, and the chain continues;</item>
/// <item>a packet is TRANSIENT: the framework never buffers, replays or
/// persists one. A mod that needs state on a late joiner re-sends it on
/// <see cref="IModContext.PlayerJoined"/>, or keeps it in
/// <see cref="IModState"/> / <see cref="IModData"/>.</item>
/// </list>
///
/// Every call that can refuse — declaring, retiring, sending — answers false
/// and writes one framework log line naming the reason, instead of silently
/// doing nothing. A drop is accepted loss: nothing is queued and nothing is
/// re-sent. The rate limit bounds what a PEER can make this copy do: a local
/// run (the host's own copy, or an
/// <see cref="ModPacketDelivery.EveryMember"/> reporter's own run) is the mod's
/// own call in its own frame and spends no budget — the tunnel's local fire
/// spends one only because it rides the receive path.
/// </summary>
[ApiStability(ApiStabilityLevel.Experimental)]
public interface IModPackets
{
	/// <summary>True when this mod declares <see cref="ModPermission.SendNetworkMessage"/>.</summary>
	bool CanSend { get; }

	/// <summary>
	/// Declare one packet for this mod id. Returns false (with a framework log)
	/// when the mod lacks <see cref="ModPermission.SendNetworkMessage"/>, when
	/// the id is not a canonical lower-case id, when the declaration carries no
	/// handler or more handlers than the framework allows, or when this mod
	/// already declares that id. Register during <see cref="ICuoMod.Bind"/>:
	/// the declaration is the same on every side that runs the mod, and the
	/// chain a frame is routed to is the one declared by the receiving copy.
	/// </summary>
	bool Register(ModPacket packet);

	/// <summary>
	/// Drop a declaration. A frame that arrives afterwards for that packet is
	/// an unknown packet id and is dropped with a log; a copy that still holds
	/// the declaration keeps routing it, so a mod retires a packet only when it
	/// stops sending it too.
	/// </summary>
	bool Unregister(string packetId);

	/// <summary>True when this mod declares that packet id.</summary>
	bool IsRegistered(string packetId);

	/// <summary>The packet ids this mod declares, in registration order.</summary>
	IReadOnlyCollection<string> PacketIds { get; }

	/// <summary>
	/// Guest: report the packet to the host's copy of this mod. On the host the
	/// call is local rather than on the wire — the host's own copy is the only
	/// one it could reach — where <see cref="IModNetwork.SendToHost"/> is a
	/// no-op, because the tunnel has no packet identity to run a chain for.
	/// Returns false (with a log) when the mod lacks the
	/// permission, the packet is not declared, this side may not send it
	/// (<see cref="ModPacketSender"/>), the declaration excludes the sender and
	/// there is no other side to reach (<see cref="ModPacketDelivery.EveryOtherMember"/>),
	/// the value is null or cannot be encoded inside the framework's 64 KiB rail,
	/// there is no active session, or a
	/// <see cref="ModPacketStage.Validate"/> handler refused the local run.
	/// </summary>
	bool SendToHost(string packetId, ModValue value);

	/// <summary>
	/// Host only: send the packet to one member's copy of this mod. Returns
	/// false (with a log) on the same checks as <see cref="SendToHost"/>, plus:
	/// a guest has no peer channels, and a
	/// <see cref="ModPacketDelivery.HostOnly"/> packet never runs on a member.
	/// </summary>
	bool SendToPeer(ulong steamId, string packetId, ModValue value);

	/// <summary>
	/// Host only: send the packet to every member's copy of this mod. Whether
	/// the host's OWN copy runs it is the declaration's answer and not this
	/// call's: <see cref="ModPacketDelivery.EveryMember"/> runs it (locally,
	/// before the frame leaves), <see cref="ModPacketDelivery.EveryOtherMember"/>
	/// does not, and a <see cref="ModPacketDelivery.HostOnly"/> broadcast is
	/// refused. Returns false (with a log) on the same checks as
	/// <see cref="SendToPeer"/>.
	/// </summary>
	bool Broadcast(string packetId, ModValue value);
}
