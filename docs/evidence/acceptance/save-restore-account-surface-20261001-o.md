# Acceptance record — S4.2 the restore account becomes player-visible

- Ticket: `save-restore-account-surface` — verdict: **stays in `review/`** (row 2 unproven: the
  claim-refused multi-client scenario needs an IP-direct roster and was not staged; the other rows
  pass)
- Batch: `20261001-o` (the batch `20261001-m` plan's Run B) — siblings
  `save-multiplayer-restore-and-backups`, `save-guest-restore-claim-and-legacy-store-retirement`,
  `save-new-player-starting-supplies`, `save-run-clock-not-sent`, `restore-account-arm-release`
- Commit: `c3e2c6fc` · Deployed artifact: `CasualtiesUnknownOnline.dll`, ProductVersion
  `0.1.0+c3e2c6fcc5d5ae482c99177224cc95d83e8935cf`
- Run: 2026-10-01 11:42 → 12:01 · Host: physical machine (Steam) · Guests: the primary sandbox and the
  alternate sandbox (third client)
- Dependencies: `steam`, `game`, `deploy`, `sandboxie`, `sandbox-alt`, `hotrepl`, `input`, `capture`,
  `logs`, `artifacts`
- Artifacts: the ids below, in the directory named by `acceptance-artifacts-dir`

| # | Row | Class | Verdict | Evidence |
|---|---|---|---|---|
| 1 | A clean continue | machine + residual | **pass** | the host console read `CUO restored world w-20261001-3315: nothing was lost.` and `world w-20261001-3315 restored from the live snapshot` (`o-host-console-after-restore1.json`, `o-host-console-after-restore3.json`); the suite half is `WorldRestoreReportTests.CleanContinue_ReportsAppliedWithNothingLost` and `CommandConsoleSaveTests.RestoreReport_OfACleanRestore_IsOneLineAndSaysNothingWasLost` in `o-s4-suite.txt` |
| 2 | Two present players sharing a transport key | machine | **unproven** | not staged: the scenario needs an IP-direct roster with a shared display name; `WorldRestoreReportTests.TwoPresentPlayersSharingAKey_ReportTheRefusedCharacterInTheItemizedAccount` is green in `o-s4-suite.txt`, but the ticket's own limits name this live refusal as the multi-client half this run had to check |
| 3 | A damaged live snapshot, opened from a backup | machine | **pass** | the live manifest was replaced with `{ not json` as the declared setup (original kept: `o-live-manifest-before-damage.json`); the console then read `CUO restored world w-20261001-3315 with 2 damaged item(s); the console history names them.` and the itemized entry `ManifestUnreadable: the live snapshot could not be read, so backup mid-run-20261001-035032.cuoz was opened instead` (`o-host-console-after-restore2.json`), and the log records `Opened snapshot for world w-20261001-3315 in state BackupFallback` (`o-host-log-excerpt.txt`); the suite half is `WorldRestoreReportTests.BackupFallback_NamesTheBackupInBothTheSummaryAndTheItemizedAccount` and `WorldSaveLogLineTests.RestoreFromABackup_NamesTheBackupAndItsSourcePath` |
| 4 | A continue with no world at all | machine | **pass** | the ticket's own limits classify the console rendering of every disposition as machine-verified: `WorldRestoreReportTests.ContinueWithoutAWorld_ReportsARefusal`, `CommandConsoleSaveTests.RestoreReport_OfARefusal_NamesTheReasonTheClickDidNothing`, `.RestoreReport_OfARefusalWithDamage_KeepsTheReasonInTheLineAndTheItemsInTheHistory` and `WorldSaveCompositionTests.ProductionRoot_WiresTheContinueAccountToTheConsole` are green in `o-s4-suite.txt` |
| 5 | An attempt applied and then abandoned | machine + partial | **pass** | `WorldRestoreReportTests.AbandonedAttempt_ReportsASecondTimeWithTheAbandonment` in `o-s4-suite.txt`; the run itself did not stage the abandonment |
| 6 | A damaged restore as the player sees it | machine + residual | **pass** | the notification half is the console line quoted in row 3 (one disposition line, the itemized line in the history, marked non-notifiable by the suite case `CommandConsoleSaveTests.RestoreReport_OfADamagedRestore_IsOneNotificationWithTheItemsInTheHistory`); residual: how the notification LOOKS in the window |
| 7 | A skipped entry's content id | machine | **pass** | `DamageReportLineTests` (entry, file, backup-fallback and id-less repository entries) in `o-s4-suite.txt`; the run's own live damage was a manifest-level entry, so it names the file, the reason and the detail but carries no content id |
| 8 | A cut and a restore of the same snapshot, one line each | machine | **pass** | the live halves are the cut line `cut written (world w-20261001-3315 at revision 1445 …)` (`o-host-save-cut1.json`) and the restore line `world w-20261001-3315 restored from the live snapshot` (`o-host-console-after-restore1.json`); the equality half is `WorldSaveLogLineTests.Cut_WritesOneInformationLineWithTheKindPhaseRevisionAndCounts`, `.Restore_WritesOneInformationLineWithTheKindPhaseRevisionAndTheSameCounts`, `.LayerEndCut_LogsTheRowsTheArchiveHoldsNotTheOnesTheKernelStillHad` in `o-s4-suite.txt` |
| 9 | A cut that wrote nothing | machine | **pass** | `WorldSaveLogLineTests.RefusedCut_IsLoggedAsARefusalAndWritesNoCommitLine` in `o-s4-suite.txt` |

## Residuals for the user

- Row 6: the notification's look — the closed console's placement, fade timing and the 680 px line
  against a long headline; the run captured the line, not a frame of the window.

## Limits

- Row 2 is the one row the ticket's own limits leave to a live multi-client pass, and the run could not
  build its IP-direct duplicate-name roster; it is `unproven`, and the mechanism behind it is green in
  this batch's suite.
- Rows 4 and 5 are judged on this batch's suite (the ticket's limits classify the console rendering of
  all four dispositions as machine-verified); no live refusal or abandonment was staged, so their
  rendering path has no screenshot or console dump from this run.
- A damaged-restore fallback was produced by a declared disk edit, not by a natural corruption.
