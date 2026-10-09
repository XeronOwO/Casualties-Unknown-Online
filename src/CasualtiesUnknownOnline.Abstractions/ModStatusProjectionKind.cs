namespace CasualtiesUnknownOnline.Abstractions;

/// <summary>
/// The semantic projection kind declared for one runtime status slot. It tells
/// the Game Adapter which typed value shape a mod is publishing through
/// <see cref="IModStatusRuntime"/>; <see cref="None"/> leaves the value alone —
/// the mod's own shape, which the framework carries and never interprets. This
/// is the migration bridge between CUCoreLib's arbitrary
/// <c>BodyStatus</c>/<c>LimbStatus</c> classes and CUO's typed, game-free runtime
/// surface: the mod still owns what its fields MEAN, but a status that is meant to
/// affect vanilla body/limb behavior must use one of the well-known projection
/// kinds so the Game Adapter can read it without seeing mod game types.
/// </summary>
public enum ModStatusProjectionKind
{
	/// <summary>The mod's own value; the Game Adapter does not interpret it.</summary>
	None = 0,

	/// <summary>A body-level <see cref="ModBodyFormulaProjection"/> contribution set.</summary>
	BodyFormula = 1,

	/// <summary>A limb-level <see cref="ModLimbProjection"/> physiology overlay.</summary>
	LimbPhysiology = 2,
}
