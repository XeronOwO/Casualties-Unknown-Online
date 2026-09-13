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
///
/// What a row's fact IS decides the verdict, and the trigger patch is the evidence:
/// a patch that reports the entity's own one-shot latch (`didHeal`, `scrapAmount`,
/// an `activated` flag) names a state the action must WRITE — and every crystal action
/// mirrors its effect's latch for exactly that reason (a replay that skipped it left the
/// peer's copy armed, so its own player could re-trigger the effect and roll a second set
/// of drops) — while a patch that reports a TRANSITION the action itself performs (the
/// bio terminal's collider enabled → disabled, i.e. its `Backgroundify`) names a state
/// this copy may be unable to write at all, and that case is NOT APPLICABLE — even
/// though the action's presentation (a sound, a nearby door) could still run.
///
/// NOT APPLICABLE therefore means "the fact is NOT in the world"; it is NOT a promise
/// that nothing was written. An action may have written what it could before concluding
/// that the named state cannot be represented — the heat toggle cycles as far as it can
/// (so the local heat state is whatever that cycle reached, not the row's), the shy
/// crystal mirrors its consumption latch and then finds no partner to swap with, and the
/// bio terminal refuses BEFORE writing because a half-unlocked terminal is worse than an
/// untouched one. What the verdict must never do is report the row as reached.
/// </summary>
internal enum TrapActionOutcome
{
	/// <summary>The action wrote the transition (or ran an idempotent effect): the row is in the world.</summary>
	Applied,

	/// <summary>The local copy already carries the state the fact names (the duplicate case): nothing was written, and the state IS in the world.</summary>
	AlreadyInState,

	/// <summary>The local entity cannot carry this fact (a generation divergence, or a game member the field contract expects is missing), so the fact is NOT in the world and a restore counts the row as refused. It does NOT promise that nothing was written: an action may have written what it could first — the heat toggle cycles as far as it can, a crystal's consumption latch is mirrored — before concluding that the named state cannot be represented. What it must never do is report the row as reached.</summary>
	NotApplicable,
}
