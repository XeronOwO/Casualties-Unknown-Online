# Medical operations are exclusive (one operator at a time)

- Status: Review
- Priority: Medium-High
- Category: Gameplay / multiplayer medical sessions (concurrency)
- Source: User ruling 2026-09-18 (design alignment session): exclusive reservation is wrong for this family — KrokMP already supports several players working the same minigame at once (pulling shrapnel, treating a dislocation), and co-op medical work is the natural expectation even though it is harder to implement.
- Related: `review/remote-interaction-local-gating.md` (the gate half of the same services), `review/remote-medical-stage-2-shrapnel-multiplayer.md`, `review/remote-medical-stage-3-other-actions.md`, `docs/backlog/watchlist/architecture-watchlist.md` (the reservation bookkeeping was the entry that had to be extracted first)

## Problem (evidence)

Cooperation on one victim was impossible by construction: the family's shared claim book
(`MedicalOperationClaims`) claimed the whole `(target, limb)` pair beside the item instance and
the operator slot, so the first operator's start refused every other operator on that limb — in
all three families (`InjectionStartCoordinator`, `ShrapnelOperationSessionService`,
`OtherMedicalOperationSessionService`) and again in the shared start re-check
(`MedicalStartRecheck`).

The native engine has no such lock. The concurrency audit of the native minigames (2026-09-19,
`reversing/Assembly-CSharp`) established one active minigame per CLIENT
(`MinigameBase.cs:239-242` returns silently when one is running), hand physics and the abort
condition on the OPERATOR's own body (`MinigameBase.cs:14-17`), no limb/body lock anywhere in
the tree, and per-kind "one per limb" facts that are ITEM-ACTION guards read at click time —
`Item.cs` `!limb.GetComponent<SplintLimb>()` / `TourniquetScript`, `limb.hasShrapnel`,
`limb.dislocated`, `limb.infectionAmount > 60f` — never an in-progress state. Any
serialization is therefore the mod's to invent, and the user's ruling is that it must not
serialize operators.

## Goal

Several players can work the same victim (and the same limb) at the same time. Each
OPERATION is an independent unit of work with its own identity, its own session and its
own outcome; the effect is applied against the authoritative limb state, so two operators
who both finish cannot double-apply one unit.

## Design direction (decide at implementation)

1. The reservation changes from "lock the limb / lock the item" to a per-UNIT claim: a
   unit is (target, limb, operation kind) plus, where the native minigame has discrete
   pieces, the piece itself (a shrapnel fragment, one minigame turn). Several units on
   one limb coexist; the same unit does not. *(The last clause is superseded by the
   2026-09-19 ruling below: two operations on one unit may start, and what does not
   coexist is the unit's RESOLUTION.)*
2. Outcome rule per kind, derived from the native semantics rather than from a blanket
   lock: a UNIQUE unit (relocating one dislocation, removing one fragment) resolves once —
   the first completion wins and the others get an immediate, precise "already handled"
   answer; a REPEATABLE effect (bandage, injection) applies per completed operation.
3. The victim's body stays locally authoritative; the completing client reports its
   terminal state, and the host's atomic commit is where two completions of one unit are
   deduplicated (the kernel command already carries an operation id — verify it is usable
   as the idempotency key before relying on it).
4. A native-concurrency audit comes first: which minigames the game itself allows two
   actors to run at once, how their progress/animations are presented, and what the native
   UI does when a second actor opens the same panel. The result decides per-kind
   eligibility instead of guessing.
5. Extract the reservation bookkeeping into its own Runtime object as part of this cycle
   (the watchlist entry), so the new per-unit rule has one owner.

**Ruling 2026-09-19 (asked and answered A).** Two operators may BOTH start the same unique
unit; the first completion settles it, and the operations still open on that unit are stopped
at that moment with a precise "already handled" answer. This settles the reading of point 1:
what does not coexist is the unit's RESOLUTION, not the operators working it — and it is why a
per-unit START claim was not built. Point 3's idempotency question is answered in "What
landed": the operation id names the OPERATION, so it cannot key the unit.

## What landed (2026-09-19: the per-unit model)

- **The cross-family limb lock is deleted.** `MedicalOperationClaims` no longer holds a
  `(target, limb)` set, and `MedicalStartRecheck` no longer has a limb half (its
  `limbClaimApplies` parameter went with it). Two operators — of the same kind or of different
  kinds — now work one limb at the same time.
- **The claims that stay are the two that ARE a fact about one holder**: the item instance (one
  item cannot be spent twice) and the operator slot (the native engine runs one minigame per
  client, so one player cannot run two operations). Their refusal strings are unchanged
  ("Item is already reserved.", "Operator already has an active medical operation.").
- **The unit is `(target, limb, kind)`, and one type owns its outcome.**
  `MedicalOperationUnitRules.ResolvesOnce` is the per-kind rule derived from the native
  semantics (table below): a UNIQUE unit settles once, a REPEATABLE effect applies per
  completed operation, and the shrapnel family's unit is the PIECE, already arbitrated by the
  shared session's per-piece ownership.
- **The first completion settles the unit and stops the others.**
  `OtherMedicalOperationSessionService.CompleteUnitOnTerminal` is the one home for "what this
  terminal does to the unit" — the two removal kinds remove only on a completed end, an
  amputation or a relocation finishes at its terminal whatever the reason, because that was
  already their committed-progress semantics — and it reports whether the unit was settled;
  `StopOtherOperatorsOnTheUnit` then answers and terminates every other operation open on the
  same unit. `OtherMedicalOperationApplier.BuildTerminal` became a pure builder, and
  `CompleteAmputation` reports whether its call settled the limb.
- **The answer is precise, and the losing operator's minigame ends.**
  `MedicalOperationTerminalReason.AlreadyHandled = 4` travels on the existing terminal
  (`MedicalOperationEndCommittedMsg`, carrying the winner's authoritative limb state), so the
  losing client ends its native minigame through the host-terminal path it already had
  (`MedicalOperationApply.OnEndCommittedReceived` → `RemoteOtherMedicalOperationHandler.OnHostTerminal`)
  and logs the family's own sentence for the kind (`MedicalOperationUnitRules.HandledReason`).
  No new wire message and no reason string on the wire; protocol 25 → 26
  (`ProtocolVersion.Current`), because a peer without the value would read the loser's terminal
  as an ordinary completion and could resolve the same unit twice. No measured latency, window
  or tolerance enters any verdict.
- **The idempotency question (design direction 3) is answered: the operation id is NOT the
  unit's key.** It names one operation, and two operations on one unit carry different ids.
  What makes a second resolution a no-op is the authoritative limb state the appliers already
  check (`CompleteDislocation` / `CompleteAmputation` / `OtherMedicalRemovalApplier.Remove`
  return false once the unit is no longer open) together with the sweep above.
- **Evidence**: `tests/CasualtiesUnknownOnline.Tests/Session/MedicalOperationConcurrencyTests.cs`
  pins every row below. The red was observed on the pre-change source: with `src/` reverted to
  `a8e324a2`, the three scenarios that need two operators on one limb fail with exactly
  "Target limb is already reserved." and the item-claim scenario passes (see
  `docs/evidence/selfchecks/players/medical-operation-concurrency-selfcheck.md`). The one
  pre-existing assertion that pinned the old exclusivity — the dislocation exclusivity case of
  the Stage-3 actions suite, method `Dislocation_ExclusiveLimbLeaseThenSuccess` — was deleted:
  it asserted the exact refusal this ruling removes, and its scenario now lives in the
  concurrency class with the ruling's expectation.

## Acceptance matrix

| # | Scenario | Expected | Pinned by |
|---|---|---|---|
| 1 | Two operators start on the same limb (different kinds) | Both run; both effects apply | `SameLimb_DifferentKinds_BothRunAndBothEffectsApply` |
| 2 | Two operators start the SAME unique unit | Both may start; exactly one resolution lands; the other is answered precisely and stopped | `SameUniqueUnit_BothMayStart_TheFirstCompletionStopsTheOther`, `UnitRules_SettleOnlyTheUniqueKinds` |
| 3 | Two operators on the same repeatable effect | Each completed operation applies | `SameRepeatableEffect_EachCompletedOperationApplies` |
| 4 | One item, two operators | The item cannot be consumed twice for one operation | `SameLimb_SameItem_TheSecondOperatorCannotShareTheItem`, `SameItem_SecondStart_IsRejectedWhileReserved` |
| 5 | Operator disconnects mid-operation | The unit is released; the victim's limb state stays coherent | `OperatorDisconnect_TheOtherOperationOnTheUnitContinues` |
| 6 | Third-party view | Every peer agrees on the limb's terminal state | row 2 asserts the stopped operation's terminal carries the settled limb; `ThirdParty_ReceivesStage3StateAndTerminal` |
| 7 | Native UI with a second operator | No stuck panel, no duplicated progress on one body | **not pinned by a test**: the user's dual-client acceptance. The panel-end path it depends on IS the pre-existing host-terminal path |

## Native per-kind semantics (the rule's evidence)

| kind | unique / repeatable | native evidence |
|---|---|---|
| dislocation | unique per dislocation | `DislocationMinigame.cs:110` → `limb.UnDislocate()` (`Limb.cs:211-218`) once the cut finishes; `:107` clamps `dislocationTimer` from the UI distance; `limb.dislocated` is the click-time gate |
| amputation | unique per limb | `AmputationMinigame.cs:72-93` → `limb.Dismember()` (idempotent); `Item.DoAmputate` gate `infectionAmount > 60f` |
| splint removal | unique per limb | `SplintLimb.cs:11-16` component, one per limb; `Item.cs:1481` `!limb.GetComponent<SplintLimb>()` |
| tourniquet removal | unique per limb | `TourniquetScript.cs:13-22` component, one per limb; `Item.cs:402` `!limb.GetComponent<TourniquetScript>()` |
| bandage | repeatable | `BandageMinigame.cs:134-137` writes `skinHealAmount`/`bandageSlowAmount`/… per stroke |
| injection | repeatable (fluid-limited) | `SyringeMinigame.cs:88-93` per tick `wat.Inject(...)` |
| AED / manual defib | repeatable (battery-limited) | `AEDMinigame.cs:128-141`, `ManualDefibMinigame.cs:127-140` per shock |
| shrapnel | per PIECE | `ShrapnelMinigame.cs:136` — `limb.shrapnel` is a count and each pull is permanent |

## Non-goals

- Rewriting the medical minigames or their UI (native reuse stays the rule).
- Cross-victim scheduling or a work queue.
- Anti-cheat (a client that lies about its own progress is out of scope).
