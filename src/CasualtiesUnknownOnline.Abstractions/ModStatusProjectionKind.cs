namespace CasualtiesUnknownOnline.Abstractions;

/// <summary>
/// The semantic projection kind declared for one runtime status slot. It tells
/// the Game Adapter which typed payload shape a mod is publishing through
/// <see cref="IModStatusRuntime"/>; <see cref="None"/> keeps the value opaque and
/// framework-owned. This is the migration bridge between CUCoreLib's arbitrary
/// <c>BodyStatus</c>/<c>LimbStatus</c> classes and CUO's typed, game-free runtime
/// surface: the mod still owns its payload bytes, but a status that is meant to
/// affect vanilla body/limb behavior must use one of the well-known projection
/// kinds so the Game Adapter can decode it without seeing mod game types.
/// </summary>
public enum ModStatusProjectionKind
{
	/// <summary>Opaque mod-owned status; the Game Adapter does not decode it.</summary>
	None = 0,

	/// <summary>A body-level <see cref="ModBodyFormulaProjection"/> contribution set.</summary>
	BodyFormula = 1,

	/// <summary>A limb-level <see cref="ModLimbProjection"/> physiology overlay.</summary>
	LimbPhysiology = 2,
}
