# Self-check — medical operation concurrency (per-unit settlement)

Ticket: `docs/backlog/review/concurrent-medical-operations.md`. Ruling under test: the user's
2026-09-19 answer A — two operators may both start the same unique unit, the first completion
settles it, and the others are stopped at that moment with a precise answer.

Scope: the medical operation family's claim model (injection, shrapnel, Stage-3 actions), the
start re-check, the terminal path that settles a unit, and the client-side path that ends a
losing operator's minigame.

## 1. Mechanism × change × evidence

| # | Mechanism | Change | Evidence |
|---|---|---|---|
| 1 | Shared claim book | The `(target, limb)` set is gone; the item instance and the operator slot stay (one item cannot be spent twice; the native engine runs one minigame per client) | `src/CasualtiesUnknownOnline.Runtime/Session/PlayerInteraction/MedicalOperationClaims.cs`; `SameLimb_DifferentKinds_BothRunAndBothEffectsApply`, `SameLimb_SameItem_TheSecondOperatorCannotShareTheItem` |
| 2 | Start re-check | Its limb half and the `limbClaimApplies` parameter are gone; participants + operator slot + item remain | `src/.../MedicalStartRecheck.cs` (three call sites: `InjectionStartCoordinator`, `ShrapnelOperationSessionService`, `OtherMedicalOperationSessionService`); every concurrency test goes through it |
| 3 | Per-kind outcome rule | New `MedicalOperationUnitRules.ResolvesOnce` (dislocation, amputation, splint removal, tourniquet removal settle once; bandage, injection, AED, manual defib repeat; shrapnel's unit is the piece) + `HandledReason` | `src/.../MedicalOperationUnitRules.cs`; `UnitRules_SettleOnlyTheUniqueKinds`; the ticket's native per-kind table (`reversing/Assembly-CSharp` evidence) |
| 4 | Unit settlement | `CompleteUnitOnTerminal` is the single home for what a terminal does to the unit (removals only on a completed end; amputation/relocation finish at any reason) and reports whether it settled | `src/.../OtherMedicalOperationSessionService.cs`; `SameUniqueUnit_BothMayStart_TheFirstCompletionStopsTheOther`, `SplintRemoval_TwoOperators_TheItemIsAwardedOnce` |
| 5 | Stopping the rest | `StopOtherOperatorsOnTheUnit` terminates every other operation open on the same `(target, limb, kind)` with `AlreadyHandled` | same tests: the stopped operation's terminal names its own operation id and carries the settled limb |
| 6 | Exactly-once effect | The completion does not re-apply on the sweep: the appliers' authoritative-state guards (`CompleteDislocation`, `CompleteAmputation`, `OtherMedicalRemovalApplier.Remove`) return false once the unit is closed; a late end request finds no session | `SplintRemoval_TwoOperators_TheItemIsAwardedOnce` (one splint awarded, the loser's `AwardedItem` null); `SameUniqueUnit_...` (a late end adds no second terminal) |
| 7 | Wire | `MedicalOperationTerminalReason.AlreadyHandled = 4` on the existing terminal; protocol 25 → 26; no new `NetMsg` (the sync-coverage vocabulary is untouched) | `src/.../Protocol/Messages/MedicalOperationTerminalReason.cs`, `src/.../Protocol/ProtocolVersion.cs`, `docs/decisions/active.md` #137, `docs/api/mod-api.md` |
| 8 | Client terminal path | The losing client ends its native minigame through the path that already existed (`MedicalOperationApply.OnEndCommittedReceived` → `RemoteOtherMedicalOperationHandler.OnHostTerminal`) and logs the family's sentence for the kind | `src/CasualtiesUnknownOnline.GameAdapter/MedicalOperationApply.cs`; the on-screen half is §4 |

## 2. The red, observed on the pre-change source

Procedure (reversible; the workspace was restored and hash-checked afterwards):

1. `src/` was reverted to `a8e324a2` with a path-limited stash, and the new
   `MedicalOperationUnitRules.cs` was moved aside (it does not exist at HEAD).
2. The three tests that need the new API were temporarily removed from the class (they cannot
   compile against the old source), leaving the four scenarios that use only pre-existing API.
3. `dotnet test tests/CasualtiesUnknownOnline.Tests/CasualtiesUnknownOnline.Tests.csproj --filter "FullyQualifiedName~MedicalOperationConcurrencyTests"`.

Result: **3 failed, 1 passed** — `SameLimb_DifferentKinds_BothRunAndBothEffectsApply`,
`SameRepeatableEffect_EachCompletedOperationApplies` and
`OperatorDisconnect_TheOtherOperationOnTheUnitContinues` all failed with the old claim book's
own refusal, verbatim: `Target limb is already reserved.` The fourth
(`SameLimb_SameItem_TheSecondOperatorCannotShareTheItem`) passed on the old source as well,
which is the point of keeping it: it pins the claim that does NOT change.

The pre-change expectation is also checkable from the tree's history without reverting anything:
`git show a8e324a2:tests/CasualtiesUnknownOnline.Tests/Session/MedicalOperationOtherActionsSessionTests.cs`
carries `Assert.Equal("Target limb is already reserved.", secondAck.RejectReason);` for exactly the
two-operators-one-limb scenario the new class now expects to be accepted.

## 3. Reproducible numbers (2026-09-19, this tree)

| Check | Command | Result |
|---|---|---|
| Medical family, focused | `dotnet test tests/CasualtiesUnknownOnline.Tests/CasualtiesUnknownOnline.Tests.csproj --filter "FullyQualifiedName~MedicalOperation"` | 28 passed |
| Full run with build | `dotnet test CasualtiesUnknownOnline.slnx --filter "FullyQualifiedName!~DeliveryChecklist"` | main 3304 passed, normative gates 55 passed |
| Cycle delta | same tree, tests | +7 concurrency cases, −1 deleted exclusivity case (3298 → 3304 in the main project) |
| Normative gates | `dotnet test tests/CasualtiesUnknownOnline.NormativeGates.Tests/...` | 55/56 while the cycle's delivery checklist is still being checked off (that one test IS the checklist gate) |
| Format | `dotnet format CasualtiesUnknownOnline.slnx` | exit 0 |
| Line gate | `wc -l` on the touched classes | other-medical 570, applier 539, shrapnel 541, injection service 506, claims 51, unit rules 45 — all under 600; `RemoteOtherMedicalOperationHandler.cs` stays 599 and is now the watchlist's "at the limit" entry |

## 4. What this proves, and what it does not

Proven here (headless, through the production composition root, the real packet handlers and
two in-process nodes on a fake transport):

- the host admits two operations on one limb, of the same kind and of different kinds, and both
  effects land on the authoritative limb;
- the per-kind settlement rule, the `AlreadyHandled` terminal its losers receive, and the
  authoritative limb state that terminal carries;
- exactly-once settlement: one award for two removal completions, no second resolution from a
  late end request, and the loser's item untouched;
- the disconnect case: the operation that stays finishes the unit;
- the claims that remain still refuse (item, operator slot).

NOT proven here, and deliberately left to the user's dual-client acceptance:

- what the native minigame panel actually looks like on a second operator's screen at the moment
  the sweep ends it (the tests drive the host arbitration and the client's event path, not a
  rendered Unity panel);
- frame-level timing of a sweep arriving while the loser is mid-gesture, and the feel of two
  operators working one limb at real latency;
- any third-party rendering of concurrent work (the audit found the native UI shows nothing
  about an in-progress operation on another body, so there is nothing new to render);
- the TARGET-BODY branch the start still goes through: the test composition carries no live capture
  (`ILocalCharacterCapture.HasLiveCapture` is false, the default `UnavailableLocalCharacterCapture`),
  so each target answers from its seeded local snapshot while production reads the live body and
  refuses when it cannot — neither that refusal nor the shrapnel count taken from a live body is
  exercised here (that seam is covered by `interaction-gate-authority-selfcheck.md`).

The tests are two in-process nodes over a fake transport: no Unity, no Steam, no rendering.
"The suite is green" is not "the screen looks right".

## 5. Accepted limitations (recorded, not silently inherited)

- **The reason travels as an enum, not a string.** The losing client derives its sentence from
  `(reason, kind)` through `MedicalOperationUnitRules.HandledReason`; no new UI was built
  (native-UI reuse stays the rule) and the operator's existing refusal surface is a log line,
  exactly like the start refusals of this family.
- **A loser's late end request is dropped silently.** Its session is already gone and it has
  already been answered, so no second terminal is sent.
- **The operator's own body facts and the item authority stay host-side** (unchanged from
  `review/remote-interaction-local-gating.md`), and a client that lies about its own progress
  remains the anti-cheat non-goal.
- **Row 7 of the ticket's matrix** (the native UI with a second operator) is verified by the
  user's acceptance, not by these tests.

## 6. Independent adversarial review (2026-09-19)

A fresh-context subagent reviewed the frozen tree (FULL tier): no blocker, no major, 6 minor + 3
nit. It reproduced every number, built a detached `a8e324a2` worktree to derive the baseline 3298,
and wrote three extra adversarial probes of its own (a stopped operation cannot be re-ended; a
timeout is not a completion; a target disconnect is not a completion — all passed). Disposition,
all inside the same commit:

| # | Finding | Disposition |
|---|---|---|
| 1 | `OtherMedicalOperationSession`'s summary still stated the removed exclusivity and the deleted limb arbitration | fixed — the summary now states the unit model |
| 2 | `RemoteOtherMedicalOperationHandler`'s summary read as a restatement of the removed exclusivity | fixed — it now says this CLIENT holds one session (the native one-minigame-per-client limit) |
| 3 | the older Stage-3 ticket and self-check still described the exclusive limb lease with no superseding pointer | fixed — a superseded pointer was added to both |
| 4 | acceptance-matrix row 7 carried prose in its "Pinned by" cell | fixed — the cell now says it is NOT pinned by a test |
| 5 | the watchlist's "600 → 595 / 600 → 553" figures are not reproducible from any landed state | fixed — labelled as session measurements, with the current tree's figures beside them |
| 6 | the tests answer the target body from a seeded snapshot, so production's live-capture refusal branch is unexercised | recorded — §4 above |
| 7 | the pre-ruling design-direction clause contradicted the ruling | fixed — superseded pointer on the clause |
| 8 | the deleted case's `limb.Pain > 0f` lost its home | fixed — the unique-unit test now lands BOTH operators' hits on the shared unit and asserts the pain grows |
| 9 | the new `AlreadyHandled` log "sits in the injection-family path that can never receive it" | **rejected** — `MedicalOperationApply` is the shared client apply path: `MedicalOperationSessionService` forwards `_other.EndCommittedReceived` (and `_shrapnel`'s) into the same `FireEndCommittedReceived`, so Stage-3 terminals reach it |

The reviewer also flagged that the red is self-declared (it declined to revert the tree); §2 now
carries the `git show a8e324a2:...` pointer that makes the pre-change expectation checkable without
reverting anything.
