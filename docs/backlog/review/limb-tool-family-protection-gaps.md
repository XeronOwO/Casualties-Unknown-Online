# The limb-tool family's two protections are weaker than its record says

- Status: Review
- Priority: High
- Category: Player interaction / cross-player item use (limb tools)
- Source: the independent NARROWED adversarial review of the entry-mapping record cycle (2026-10-08),
  findings M1 and m2, each reproduced from the tree below. The family landed one commit before that
  cycle (`be252c44`, corrected by `957b0deb`), so both gaps belong to it, not to the record cycle —
  which is where the record's own completeness gap was closed (decision 246 states both).
- Related: `docs/decisions/active.md` row 246 (the settled rule this ticket protects),
  `docs/evidence/selfchecks/players/cross-player-native-limb-tool-semantics-selfcheck.md` §4/§5,
  `review/mod-cross-player-native-semantics.md` (the family's parent ticket),
  `docs/en/contributing/gates-and-rules.md` ("Writing a new gate")

## What landed (2026-10-08)

- `NativeLimbTarget` now carries TWO verdicts behind one guard: `ResolveNamed` answers with the limb the
  request named or with nothing, and `Resolve` is `ResolveNamed ?? PickMostInjured` — so the family's
  question and the sibling chains' auto-select cannot drift apart. `NativeLimbToolApply` asks the NAMED
  one, and a limb this body cannot serve ends the run with a log naming the item and the index
  (`[ItemUse] the limb tool on {Type} (id {ItemId}) cannot run: limb {Limb} of the request is not a
  usable limb of this body (gone, dismembered, or past its layout) — refused rather than applied to
  another limb.`) instead of landing on a limb nobody picked.
- `LimbToolChainGateTests` gained two facts and one contract case and tightened the resolve assertions
  inside its existing run fact (5 facts / 11 cases → 6 / 12): the apply's resolve is the named one and the
  fallback name is ABSENT from that file (this is the cycle's red — it fails on the pre-fix tree), the
  isolation is now read from `LocalUseItemEligibility`'s `IsUseItem`/`FamilyOf` where the gate actually
  lives with the file's own alias spellings resolved, and `IsMedicalLimbUseItem` is asserted as the place
  the limb rule IS asked; `TheMemberScoping_ReadsTheNamedMemberAndNotItsNeighbour` pins the scoping with a
  synthetic source.
- Evidence: `docs/evidence/selfchecks/players/limb-tool-named-limb-refusal-selfcheck.md` (mechanism
  inventory, whole-family audit, the two measured mutations, the limits), and decision 246's second half.
- Not in this cycle, and landed in the next one: the TOPICAL world drag kept its most-injured-limb
  resolve here; the user's ruling that a topical application is the medical view's action closed that
  sibling reach too (`docs/evidence/selfchecks/players/topical-entry-and-named-limb-selfcheck.md`,
  decision 246). What stays an acceptance row is the native half of both changes — whether the refusal
  happens on a live body, and what its log says there.

## Gap 1 — a NAMED but unusable limb still falls back to the most injured one

The family's rule is that the limb IS the action: the operator picks it in the wound view, the request
carries it, and the host refuses a request that names none (`PlayerItemUseService`, `if (limbIndex < 0)` —
"is a limb tool but the request named no limb"). The run that lands on the patient resolves whatever
number arrives through `NativeLimbTarget.Resolve` (`src/CasualtiesUnknownOnline.GameAdapter/`), whose rule
is "the operator's pick when it is a real, attached limb of this body, **otherwise the most injured
one**". So the fallback the user rejected for this family is still the affected side's answer whenever
the named limb cannot be served:

- The pick has gone stale by the time the run lands — a limb dismembered in between (a cross-player
  amputation is a supported operation) is no longer attachable.
- A request names an index the patient's body does not have: the host validates only `limbIndex < 0`
  (`PlayerItemUseService` publishes `LimbIndex = limbIndex` unchanged), so an index past
  `body.limbs.Length` reaches the affected side.

The consequence is the one decision 246 names as the defect: the tool silently lands on a **different**
limb than the operator picked, and nothing reaches the operator or the host.

The fallback is NOT wrong for the class's other callers, which is why the fix belongs to this family:
`NativeLimbTarget`'s own doc comment says it is the rule the two migrated limb-use chains share
("the same automatic rule the host applies for the heal slice"), and that rule is pinned as a boundary
rather than a comment by `InjectionSemanticsTests.LimbRule_TheRequestedLimbWinsAndAnInvalidOneFallsBackToTheMostInjured`
over the Runtime's pure `RemoteHealApplication.ResolveLimbIndex` (its `-1` case is a legal auto-select
for an injection). Refusing an unusable limb is therefore a rule for the limb-tool family alone.

## Gap 2 — the isolation pin's scan surface is one file, so the regression it guards can return

`LimbToolChainGateTests` asserts two things about the isolation: no `LimbToolAdmission` identifier in
the world-drag file, and the use service keeps its `limbIndex < 0` refusal. Its scanned sources are the
Runtime admission files plus `src/CasualtiesUnknownOnline.GameAdapter/CrossPlayerDragUse.cs` — but the
gate the world drag actually asks is `LocalUseItemEligibility.IsUseItem`, which is **not** in that
surface. Re-adding `LimbToolAdmission.IsLimbTool` to `IsUseItem` (exactly what `957b0deb` removed) would
leave this gate green as long as `CrossPlayerDragUse.cs` kept calling `IsUseItem`. Second, the check walks
`DescendantNodes()` of the syntax tree, which does not descend into documentation-comment trivia, so a
`<see cref="LimbToolAdmission"/>` in that file's comments is invisible to it too.

## What a fix has to decide

- **Where the refusal lives.** The host cannot answer it: it holds no live limb layout of the patient's
  body, which is why the resolve happens on the affected side. So either the affected side refuses the
  run (and reports nothing, like its other local refusals) or the request gains a fact that makes the
  pick verifiable at the host — a design question, not a patch.
- **Whether the decision becomes a pure rule.** `RemoteHealApplication.ResolveLimbIndex` is the
  precedent: a pure Runtime rule over the patient's limb rows, with the adapter only feeding it. The
  limb-tool family needs the same shape with the opposite verdict for an unusable named limb, or a
  named strict resolve in the adapter.
- **How this change gets its red.** The family's apply needs a game process (`Body.limbs`), so an L0
  case can only reach a decision extracted out of it, and a newly introduced rule fails to compile
  before it exists — a compile error is not a red. The two workable shapes are the family's own
  precedent (a mutation measured against a rebuilt tree, self-check §4) plus the acceptance row, or a
  source-walk gate whose negative sample is the reverted fix.
- **The widened pin's matcher.** The assertion has to be scoped to `IsUseItem`/`FamilyOf` inside
  `LocalUseItemEligibility` (the same file's `IsMedicalLimbUseItem` legitimately asks
  `LimbToolAdmission.IsLimbTool`), with its own positive and negative samples.

## Acceptance

Two clients on the deployed artifact, the operator driving the wound-view gesture on a teammate: (a) the
picked limb is served — the tool's effect and its component land on that limb; (b) with the picked limb
dismembered between the pick and the run, or with a request naming an index the patient's body does not
have, the run is REFUSED with a log naming the item and the patient, and no limb receives an effect it
was not picked for. The reading is the treated side's own limb state plus the operator's and the third
peer's view of it.

## Evidence reproduced from the tree (2026-10-08, the PRE-FIX state this ticket was filed from)

- `NativeLimbTarget.Resolve`'s full body: the guarded return of `body.limbs[requestedLimbIndex]`, then
  the `skinHealth + muscleHealth` minimum loop over the attachable limbs.
- `PlayerItemUseService`: `if (limbIndex < 0)` refused with the warning above, and the published
  `PlayerItemResultMsg` carrying `LimbIndex = limbIndex` with no other check on it.
- `LimbToolChainGateTests`: `DragUseFile` = `CrossPlayerDragUse.cs`, `LimbToolChainSources` = the Runtime
  admission files, and the `DescendantNodes()` identifier walk.
- `InjectionSemanticsTests` (the fallback pinned as the injection chain's own rule) and
  `RemoteHealApplication.ResolveLimbIndex` (its Runtime half).
