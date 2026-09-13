namespace CasualtiesUnknownOnline.Runtime.Session.World;

/// <summary>
/// What a shared trap/entity state action did with ONE fact — the verdict the
/// Game Adapter's action library (<c>TrapStateActions</c> / <c>CrystalStateActions</c>)
/// returns instead of a bool, because a bool cannot tell "the local copy already
/// carries this state" (a duplicate from the two-trigger race — the state the row
/// names IS in the world) from "this entity cannot carry the fact at all" (a
/// generation divergence — the fact is NOT in the world).
///
/// The restore's live-write account needs that distinction, and it is not
/// hypothetical: the effect list is rolled PER CRYSTAL and the mimic is one of
/// seventeen weighted effects (weight 8 of 139), with `SetUpEffects` destroying
/// about seven crystals in ten before it sets any effect at all
/// (CrystalBehaviour.SetUpEffects, CrystalBehaviour.cs:83-102, the mimic row at
/// :196) — so a restored CrystalMimicTriggered row can land at the expected
/// position on a crystal of the expected kind that carries no mimic effect: an
/// entity that EXISTS and still cannot carry the fact. (The seven-in-ten case is
/// the older one — no entity at all, which the account already refused.) Under the
/// old bool contract the surviving-but-incapable row counted as applied, so the
/// divergence never reached the player's restore report.
/// </summary>
internal enum TrapActionOutcome
{
	/// <summary>The action wrote the transition (or ran an idempotent effect): the row is in the world.</summary>
	Applied,

	/// <summary>The local copy already carries the state the fact names (the duplicate case): nothing was written, and the state IS in the world.</summary>
	AlreadyInState,

	/// <summary>The local entity cannot carry this fact (a generation divergence, or a game member the field contract expects is missing): nothing was written and the fact is NOT in the world, so a restore counts the row as refused.</summary>
	NotApplicable,
}
