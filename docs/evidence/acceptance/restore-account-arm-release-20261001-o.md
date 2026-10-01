# Acceptance record — the restore account's armed halves

- Ticket: `restore-account-arm-release` — verdict: **moved to `done/`**
- Batch: `20261001-o` (the batch `20261001-m` plan's machine-only ticket) — siblings
  `save-multiplayer-restore-and-backups`, `save-guest-restore-claim-and-legacy-store-retirement`,
  `save-restore-account-surface`, `save-new-player-starting-supplies`, `save-run-clock-not-sent`
- Commit: `c3e2c6fc` · Deployed artifact: `CasualtiesUnknownOnline.dll`, ProductVersion
  `0.1.0+c3e2c6fcc5d5ae482c99177224cc95d83e8935cf` (deployed and hash-verified in this run)
- Run: 2026-10-01 — preflight `0` (11 present); build; the focused suite below; the full suite
  (`o-full-suite.txt`: 300/300 gates, 4547/4547 main); `format`; deploy + `verify-deploy`; no runtime
  row exists for this ticket, so a client session is not part of its judgement
- Dependencies: `dotnet`, `game`, `deploy`, `logs`, `artifacts` (the client capabilities were not needed
  for the rows; they were present and used by the sibling tickets of this batch)
- Artifacts: the ids below, in the directory named by `acceptance-artifacts-dir`

| # | Row | Class | Verdict | Evidence |
|---|---|---|---|---|
| 1a | The session ends while the restore still owes the world-fact half | machine | **pass** | `WorldSaveContinueTests.SessionEnd_ReportsTheWorldFactHalfTheRestoreStillOwed` — passed in the batch's focused run (`arm-release-suite.txt`) |
| 1b | The same, with only the adapter's native handover armed | machine | **pass** | `.SessionEnd_ReportsTheWorldFactHalfEvenWhenOnlyTheAdapterHoldsIt` — passed (`arm-release-suite.txt`) |
| 1c | The session ends while the account owes the world-entity half | machine | **pass** | `WorldEntityProjectionTests.SessionEnd_ReportsTheWorldEntityHalfTheRestoreStillOwed` — passed (`arm-release-suite.txt`) |
| 1d | One armed half released along two paths | machine | **pass** | `WorldRestoreAuditTests.AHalfThatReportsTwice_IsCountedOnce` — passed (`arm-release-suite.txt`) |
| 1e | A half the open account does not owe reports | machine | **pass** | `.AHalfTheAccountDoesNotOwe_DoesNotStandInForTheOneItWaitsFor` — passed (`arm-release-suite.txt`) |
| 1f | The applier's list of owed halves | machine | **pass** | `.LiveWorldHalves_NameTheWritersThatAreActuallyArmed` — passed (`arm-release-suite.txt`) |
| 2a | A restore is superseded by a new Continue | machine | **pass** | `WorldSaveContinueTests.TryContinue_SupersedesThePreviousAttemptsItemReconcile` — passed (`arm-release-suite.txt`) |
| 2b | A layer-end cut replaces the armed item half | machine | **pass** | `.ALayerEndRestore_DropsTheItemHalfBeforeTheAccountOpens` — passed (`arm-release-suite.txt`) |
| 2c | An applied attempt is abandoned | machine | **pass** | `.AbandonRestore_CancelsTheArmedItemReconcileToo` — passed (`arm-release-suite.txt`) |
| 2d | The session ends, item expectation ends through the service's own subscription | machine | **pass** | `RestoredWorldItemContractTests.ASessionEnd_EndsTheItemExpectationThroughTheServicesOwnSubscription` — passed (`arm-release-suite.txt`) |

## Residuals for the user

- None; every row is a machine verdict.

## Limits

- The focused run that carries the ten cases is this batch's own build/output step: 66 tests across the
  four named classes, 0 failed (`arm-release-suite.txt`), and each of the ten rows' cases was re-read in
  that output before this record was written.
- The adapter's own session binding (`GameAdapterSessionBinding.OnSessionEnded` releasing the native
  handover) remains read-only reviewed, as the ticket's own Verification limits state; the contribution
  it enables is made by the save layer and is what the suite pins.
- The session-start rows of this family are judged by this batch's sibling records; this ticket keeps
  no in-game row.
