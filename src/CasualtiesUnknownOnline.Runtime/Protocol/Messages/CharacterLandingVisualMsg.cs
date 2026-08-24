using ProtoBuf;

namespace CasualtiesUnknownOnline.Runtime.Protocol.Messages;

/// <summary>
/// ONE player-character landing presentation event (Body.HandleGroundedState's
/// "became grounded" branch, Body.cs:2713-2740): the owner's local simulation
/// already played the <c>Grounded</c> clip and, on a hard enough fall, spawned
/// the <c>DustSmall</c>/<c>DustBig</c> cloud. This event lets the peers replay
/// the same one-shot visual on the owner's render clone with the same cloud
/// size, anchor position and horizontal emitter velocity. Star semantics:
/// guest → host report, host fires the event and relays to the other members
/// (source excluded); host → guest relay fires the replay. One landing = one
/// message; there is no snapshot fallback for a transient one-shot visual (a
/// lost event is acceptable presentation degradation).
/// </summary>
[ProtoContract]
public sealed class CharacterLandingVisualMsg
{
	/// <summary>No landing dust was spawned (a soft landing still replays the Grounded pose).</summary>
	public const byte CloudNone = 0;

	/// <summary>The native <c>DustSmall</c> cloud (Body.cs:2722-2725).</summary>
	public const byte CloudSmall = 1;

	/// <summary>The native <c>DustBig</c> cloud (Body.cs:2718-2721).</summary>
	public const byte CloudBig = 2;

	/// <summary>The landing player's SteamId (stamped by the reporter; the host stamps its own on broadcast).</summary>
	[ProtoMember(1)]
	public ulong OwnerSteamId { get; set; }

	/// <summary>Which native cloud the source spawned: 0 none, 1 small, 2 big.</summary>
	[ProtoMember(2)]
	public byte CloudSize { get; set; }

	/// <summary>The world-space cloud anchor reported by the source (the receiver uses the owner clone's live body when it exists).</summary>
	[ProtoMember(3)]
	public NetVector2Msg Position { get; set; } = new();

	/// <summary>The source's horizontal velocity at landing — the cloud emitter velocity the native call used.</summary>
	[ProtoMember(4)]
	public float VelocityX { get; set; }
}
