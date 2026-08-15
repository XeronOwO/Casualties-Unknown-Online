using ProtoBuf;

namespace CasualtiesUnknownOnline.Runtime.Protocol.Messages;

/// <summary>
/// An enemy proximity side effect fired on the LOCAL body (local compute —
/// the game's own Update/OnWillRenderObject already mutated the player):
/// carries the post-effect terminal state so every peer applies the exact
/// same values (exact rebuild, never a delta). Bidirectional star semantics,
/// the same as EnemyBite: guest → host report (the victim is the reporter);
/// host → guest broadcast relay (the victim is <see cref="VictimSteamId"/>).
/// Only the kind-relevant fields are populated; the 1 Hz character snapshot
/// stays the fallback for the other body fields.
/// </summary>
[ProtoContract]
public sealed class EnemyEffectMsg
{
	/// <summary>The affected player (the reporter's own SteamId for a guest report).</summary>
	[ProtoMember(1)]
	public ulong VictimSteamId { get; set; }

	/// <summary>The proximity side effect that fired.</summary>
	[ProtoMember(2)]
	public EnemyEffectKind Kind { get; set; }

	/// <summary>ElderThornback tick/defeat: the post-effect horror level.</summary>
	[ProtoMember(3)]
	public float HorrifiedLevel { get; set; }

	/// <summary>ElderThornback tick: the post-effect focus level.</summary>
	[ProtoMember(4)]
	public float FocusedLevel { get; set; }

	/// <summary>ElderThornback tick: the post-effect adrenaline.</summary>
	[ProtoMember(5)]
	public float Adrenaline { get; set; }

	/// <summary>ElderThornback tick: the post-effect energy.</summary>
	[ProtoMember(6)]
	public float Energy { get; set; }

	/// <summary>ElderThornback tick: the post-effect stamina.</summary>
	[ProtoMember(7)]
	public float Stamina { get; set; }

	/// <summary>ElderThornback defeat: the post-reward happiness.</summary>
	[ProtoMember(8)]
	public float Happiness { get; set; }

	/// <summary>ElderThornback defeat: the post-reward caffeine.</summary>
	[ProtoMember(9)]
	public float Caffeinated { get; set; }

	/// <summary>Xaloris tick: the post-tick septic shock.</summary>
	[ProtoMember(10)]
	public float SepticShock { get; set; }

	/// <summary>GrabberPlant grab: the post-grab shock.</summary>
	[ProtoMember(11)]
	public float Shock { get; set; }

	/// <summary>GrabberPlant grab: the post-grab eye panic timer.</summary>
	[ProtoMember(12)]
	public float EyePanicTime { get; set; }
}
