namespace CasualtiesUnknownOnline.Abstractions;

/// <summary>
/// The limb vitals <see cref="IModEffectLimb"/> reads and writes, one member per
/// write the game's own shipped topical effects perform (the census in
/// <c>docs/backlog/todo/mod-authored-effects.md</c>). The names are CUO's; each
/// member's documentation names the game's own field it stands for.
///
/// <para>
/// Disinfection is deliberately NOT a member: the game's own
/// <c>Limb.SetDisinfect</c> is a method with its own bookkeeping, so it is
/// <see cref="IModEffectLimb.Disinfect"/> instead of a field write.
/// </para>
/// </summary>
[ApiStability(ApiStabilityLevel.Advanced)]
public enum ModLimbVital
{
	/// <summary>Pain (<c>Limb.pain</c>).</summary>
	Pain,

	/// <summary>Muscle health (<c>Limb.muscleHealth</c>).</summary>
	MuscleHealth,

	/// <summary>Skin health (<c>Limb.skinHealth</c>).</summary>
	SkinHealth,

	/// <summary>Skin healing accumulated by a topical application (<c>Limb.skinHealAmount</c>).</summary>
	SkinHeal,

	/// <summary>Infection amount (<c>Limb.infectionAmount</c>).</summary>
	Infection,

	/// <summary>How much a bandage slows this limb (<c>Limb.bandageSlowAmount</c>).</summary>
	BandageSlow,

	/// <summary>Bleed amount (<c>Limb.bleedAmount</c>).</summary>
	Bleed,
}
