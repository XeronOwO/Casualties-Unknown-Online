using ProtoBuf;

namespace CasualtiesUnknownOnline.Runtime.Protocol.Messages;

/// <summary>
/// ONE player-character attack animation one-shot (Body.Attack's
/// <c>attackAnim</c> prefab — ClawAnim / SwingAnim / LaserAnim, Body.cs:1913-1920).
/// The owner's local simulation already instantiated the visual; this event lets
/// the peers replay it on the owner's render clone with the same prefab, facing
/// and attack direction. Star semantics: guest → host report, host fires the
/// event and relays to the other members (source excluded); host → guest relay
/// fires the replay. One attack animation = one message; there is no snapshot
/// fallback for a transient one-shot visual (a lost event is acceptable
/// presentation degradation).
/// </summary>
[ProtoContract]
public sealed class CharacterAttackAnimMsg
{
	/// <summary>The acting player's SteamId (stamped by the reporter; the host stamps its own on broadcast).</summary>
	[ProtoMember(1)]
	public ulong OwnerSteamId { get; set; }

	/// <summary>The Resources name of the attack-anim prefab the source instantiated ("ClawAnim", "SwingAnim", "LaserAnim", …).</summary>
	[ProtoMember(2)]
	public string Prefab { get; set; } = "";

	/// <summary>The world-space arm/hand position used as the visual's anchor (the receiver uses the owner clone's live arm when it exists).</summary>
	[ProtoMember(3)]
	public NetVector2Msg Position { get; set; } = new();

	/// <summary>The normalized attack direction the source's visual was rotated toward.</summary>
	[ProtoMember(4)]
	public NetVector2Msg Direction { get; set; } = new();

	/// <summary>The source body's facing at the attack (the visual's local-scale x sign).</summary>
	[ProtoMember(5)]
	public bool IsRight { get; set; }
}
