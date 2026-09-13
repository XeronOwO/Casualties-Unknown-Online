using CasualtiesUnknownOnline.Runtime.Session.World;

namespace CasualtiesUnknownOnline.GameAdapter;

/// <summary>
/// Reflection access to the internal CrystalMimic effect (CrystalMimic.cs): the effect
/// list lives in the private CrystalBehaviour.effects field (CrystalBehaviour.cs:83-107)
/// and the one-shot latch is the private bool activated (CrystalMimic.cs:52). The
/// lookup and the latch rule are <see cref="CrystalEffectAccess"/>'s (one place for the
/// untyped list scan and the typed latch read/write); this type keeps the mimic's type
/// name and its own reasoning. The GameFieldContractTests rows lock both members against
/// a game update.
/// </summary>
internal static class CrystalMimicAccess
{
	private const string MimicTypeName = "CrystalMimic";

	/// <summary>The mimic's activated latch (false when the crystal carries no mimic).</summary>
	internal static bool IsActivated(CrystalBehaviour crystal) => CrystalEffectAccess.IsActivated(crystal, MimicTypeName);

	/// <summary>Set the latch exactly once, reporting WHICH way it did not apply:
	/// <see cref="TrapActionOutcome.Applied"/> when the latch was written,
	/// <see cref="TrapActionOutcome.AlreadyInState"/> when it was already consumed
	/// (the duplicate case — the state the row names IS in the world), and
	/// <see cref="TrapActionOutcome.NotApplicable"/> when this crystal carries no
	/// mimic effect at all or the latch member is missing — the two reasons a bool
	/// used to conflate, and the divergence a restore must count as a refused row
	/// (CrystalBehaviour.SetUpEffects rolls the list per crystal — the mimic is one
	/// of seventeen weighted effects, and about seven crystals in ten are destroyed
	/// before any effect is set, CrystalBehaviour.cs:83-102).</summary>
	internal static TrapActionOutcome TryActivate(CrystalBehaviour crystal) =>
		CrystalEffectAccess.TryActivate(crystal, MimicTypeName);
}
