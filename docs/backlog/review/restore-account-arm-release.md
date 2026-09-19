# The restore account's entity and native arms are released without a contribution

- Status: Review
- Priority: Low
- Category: Persistence / save system (the restore account)
- Source: recorded by the restore-ATTEMPT-identity cycle of
  `review/save-mid-run-consistent-cut.md` (2026-09-12), re-verified against the tree on 2026-09-17
  when that ticket moved to `review/`, and landed 2026-09-19
- Related: `review/save-mid-run-consistent-cut.md` (the pass that recorded them), decisions
  172/179/182/195 in `docs/decisions/active.md`,
  `src/CasualtiesUnknownOnline.Runtime/Session/Persistence/WorldRestoreAudit.cs`,
  `src/CasualtiesUnknownOnline.Runtime/Session/Persistence/WorldRestoreHalf.cs`,
  `src/CasualtiesUnknownOnline.Runtime/Session/Persistence/WorldSaveService.cs`,
  `src/CasualtiesUnknownOnline.Runtime/Session/World/WorldEntityKernelProjection.cs`

## What landed

The account's expectation was a bare COUNT of live-world halves and only the item arm's release
reached it, which produced one reachable defect — a session ending mid-restore left the account
awaiting a half nobody would ever report — and one latent one: a count cannot say WHICH half
reported, so a contribution from another armed half fills the slot a missing half owes and the
report names a loss the restore never had, while the half that never reported disappears from it
without a word. Both halves are now identified and accounted:

- **The account is keyed by WHICH half reports** (`WorldRestoreHalf`: `WorldFacts` — the Runtime
  world-fact tables together with the adapter's native handover, one half reported as one —
  `WorldEntities`, `WorldItems`). `BeginRestore` is handed the halves the click actually ARMED
  (`WorldRestoreApplier.LiveWorldHalves`, unchanged rule, now returning the list rather than a count),
  every contribution carries its half, and each half counts AT MOST ONCE per account.
- **A contribution the open account does not owe is refused, and one for a half that already
  reported is dropped** — both as producer-bug verdicts, the same shape the attempt-identity rule
  already gave a straggler. This is what makes a release that runs along several paths (the seam
  wrote the half, then the session ended and its owner released it again) count exactly once, and it
  is why the save layer can contribute its half unconditionally at session end.
- **Every half reports from the ONE place its arm ends.** `WorldEntityKernelProjection` contributes
  the world-entity half when it drops an armed one (session end, supersession, layer-end cut, a throw
  at the seam — a single site, and the arm is cleared first so a re-entrant release finds nothing);
  `ItemService`/`RestoredWorldItemSet` already contributed the item half; and `WorldSaveService` now
  subscribes to `SessionEnded` and contributes the world-fact/native half, stamped with the attempt
  the tables were applied by (`IWorldFactSource.AppliedRestoreSequence`) — the save layer is the only
  holder of both the tables' stamped attempt and the adapter's unstamped native handover.
- **The save layer's item dependency is narrowed to the port**: `IItemControl` →
  `IRestoredWorldItemSource` (the pending flag plus `CancelRestoredWorldItems`). The service only ever
  used those two members, and the narrowing is what makes the release's call sites pinnable by a
  suite (acceptance 2) instead of readable only.

The account's own doc (`WorldRestoreAudit`) and `docs/architecture/save-archive-format.md` §6
("a restore has halves in time") carry the rule; decision 195 records it.

## Acceptance mapping

| # | Scenario | Expected | Evidence |
|---|---|---|---|
| 1a | The session ends while the restore still owes the world-fact half (the Runtime marker set) | the account is closed WITH a report; it does not stay awaiting | `WorldSaveContinueTests.SessionEnd_ReportsTheWorldFactHalfTheRestoreStillOwed` |
| 1b | The same, with only the adapter's native handover armed (a cut that carried no Runtime world fact) | the half is still accounted — the ACCOUNT says a half is owed, not the marker | `WorldSaveContinueTests.SessionEnd_ReportsTheWorldFactHalfEvenWhenOnlyTheAdapterHoldsIt` |
| 1c | The session ends while the account owes the world-entity half (and the item half with it) | every opened half is accounted and named; the item half's release no longer stands in for the world-entity half | `WorldEntityProjectionTests.SessionEnd_ReportsTheWorldEntityHalfTheRestoreStillOwed` |
| 1d | One armed half is released along two paths | counted once | `WorldRestoreAuditTests.AHalfThatReportsTwice_IsCountedOnce` |
| 1e | A half the open account does not owe reports | refused, and the owed half still decides the report | `WorldRestoreAuditTests.AHalfTheAccountDoesNotOwe_DoesNotStandInForTheOneItWaitsFor` |
| 1f | The applier's list of owed halves | names the writers armed at the click, in order; the world-fact half always | `WorldRestoreAuditTests.LiveWorldHalves_NameTheWritersThatAreActuallyArmed` |
| 2a | A restore is superseded by a new Continue | the previous attempt's armed item set is cancelled (port wiring, not a direct call) | `WorldSaveContinueTests.TryContinue_SupersedesThePreviousAttemptsItemReconcile` |
| 2b | A layer-end cut replaces the armed item half | dropped BEFORE the account opens; the account does not owe it | `WorldSaveContinueTests.ALayerEndRestore_DropsTheItemHalfBeforeTheAccountOpens` |
| 2c | An applied attempt is abandoned (no run baseline) | the armed item set is released | `WorldSaveContinueTests.AbandonRestore_CancelsTheArmedItemReconcileToo` |
| 2d | The session ends | the item expectation ends through `ItemService`'s OWN `SessionEnded` subscription | `RestoredWorldItemContractTests.ASessionEnd_EndsTheItemExpectationThroughTheServicesOwnSubscription` |

Red before the fix (frozen pre-fix tree, tests kept): all three session-end cases failed, and the
measured shape is the awaiting-forever defect itself — at the count the pre-fix applier actually
returns for these compositions (both arms armed ⇒ 3) each left the account open with NO report
(`awaiting=True`, 0 reports). The attribution half of the defect is reproducible at a count that
omits an armed half (`expectedContributions: 2`, which is how 1c was written against the pre-fix
API): the item half's release then completed the account, and the report carried only the item's
reason with no world-entity entry at all. Both forms are red; only the second shows the count's
inability to attribute, and only the identity fixes both.

## Family sweep (the same defect, every release path)

| release path | verdict |
|---|---|
| the world-entry seam (landed rows, refusals, a throw) | reports every half it was handed, each tagged with its half; the world-entity arm is then released by the projection, whose duplicate release the account drops by design |
| `WorldRestoreApplier.TryApply` supersession and layer-end cancels | the account is closed (`AbandonRestore`) BEFORE the arms are released, or the half is not owed — no contribution wanted, by design |
| `WorldSaveService.AbandonRestore` / `TryBeginRun` | the same: `_audit?.AbandonRestore()` closes the account before the arms go |
| `RunSaveCoordinator.BeginRun` (the adapter, before `_saves.TryBeginRun`) | releases the item arm while the account may still be open, and that release IS counted — the half will never arrive; `TryBeginRun` then closes the account, so an attempt still owing another half is abandoned without a report |
| `WorldService.ResetSessionState` (`_facts.ClearPendingLiveReplay()`) and `GameAdapterSessionBinding.OnSessionEnded` (the native handover) | both release ONE OF THE TWO arms of the world-fact half at session end; neither can attribute the half on its own, which is exactly why the save layer contributes it |
| `WorldParamsService.CaptureAtEntry` / `CaptureAtBoundary` (the native handover) | run-start / layer-boundary paths the restore path bypasses, with no account open (the generation boundary returns early while a restore is pending) |
| `WorldFactLifecycle.ResetWorldDomainTables` (layer boundary) | the same: a new layer's reset, unreachable with an account open (the world-entry reset is gated on the replay having a pending half) |
| the session end | FIXED here: the projection (world entities), `ItemService` (items) and the save layer (world facts + native) each contribute |

## Recorded design positions (not work)

### A mismatch is silent once the account is closed

The identity warning only fires while an account is OPEN, because a closed or abandoned account
ignores every contribution by design, so a late writer after a completed restore is not named.
ACCEPTED as a design position — the rule that a straggler must not invent a report for a restore
that has already reported is the stronger one.

### The supersession releases the arms only when a restore actually applies

The release sits after the two content refusals (an unreadable or absent archive) and before the
kernel restore, so a refused Continue leaves whatever the previous attempt armed in place; the next
applying attempt releases it, and the pre-existing `AbandonRestore` paths cover a new run. The
invariant is exactly: "a new APPLIED restore supersedes the previous attempt".

## Verification limits

- The session-end path runs through `ISessionControl.SessionEnded`; the Runtime-side wiring is
  machine-checked (both the projection and the save layer are constructed by the suites), while the
  adapter's own session binding (`GameAdapterSessionBinding.OnSessionEnded` releasing the native
  handover) is read-only reviewed — the contribution it enables is made by the save layer, not by
  the adapter.
- The in-game effect (a session ending in the middle of a restore, and the console line the player
  would see) belongs to the user's unified acceptance pass. The report's own rendering is covered by
  `CommandConsoleSaveTests.IncompleteRestore_IsPrintedAtErrorLevel`.
- The full-suite evidence `ACancelledReconcile_ReportsTheLossInsteadOfWaitingForever` (the release
  METHOD) and the four new port-wiring cases are the machine half; the item half's live-world
  landing (the reconcile against the regenerated layer) still needs a running game.
