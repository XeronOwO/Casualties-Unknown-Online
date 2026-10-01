# Acceptance record — S4.3 starting supplies for a new player

- Ticket: `save-new-player-starting-supplies` — verdict: **moved back to `todo/`** — row 3 FAILS
  (`- Status: Todo — Rejected (row 3: a body the world has a character for announced a starting-supply
  grant before the stored character arrived; the granted item was then superseded by the restore)`)
- Batch: `20261001-o` (the batch `20261001-m` plan's Run B) — siblings
  `save-multiplayer-restore-and-backups`, `save-guest-restore-claim-and-legacy-store-retirement`,
  `save-restore-account-surface`, `save-run-clock-not-sent`, `restore-account-arm-release`
- Commit: `c3e2c6fc` · Deployed artifact: `CasualtiesUnknownOnline.dll`, ProductVersion
  `0.1.0+c3e2c6fcc5d5ae482c99177224cc95d83e8935cf`
- Run: 2026-10-01 11:42 → 12:01 · Host: physical machine (Steam) · Guests: the primary sandbox and the
  alternate sandbox (third client)
- Dependencies: `steam`, `game`, `deploy`, `sandboxie`, `sandbox-alt`, `hotrepl`, `input`, `capture`,
  `logs`, `artifacts`
- Artifacts: the ids below, in the directory named by `acceptance-artifacts-dir`

| # | Row | Class | Verdict | Evidence |
|---|---|---|---|---|
| 1 | A player who was not in the package joins a RESTORED world past its starting layer | machine + residual | **pass** | the world's live layer was 1 (past the starting layer, `o-host-runclock-before-restore1.json`); the third client was absent at the restore and rejoined after it; its console read `CUO new player: starting supplies (light) given — emergencylight.` (`o-alt-console-after-rejoin-2.json`) and its tree held one fresh `emergencylight#22377858709` (`o-alt-local-tree-after-rejoin-2.json`); the suite half is `StartingSupplyCoordinatorTests.Update_AnOmittedPlayerInARestoredWorld_IsSupplied` in `o-s4-suite.txt` |
| 2 | A player joining a running world (its first layer already behind it) | machine | **pass** | the same join: one grant, one account line, on a running world whose starting layer was behind it (`o-alt-console-after-rejoin-2.json`); the once-per-body rule is `StartingSupplyCoordinatorTests.Update_TheSameBodyIsJudgedOnce_HoweverManyFramesRun` plus the tracker cases in `o-s4-suite.txt`. The run did not separately stage a non-restored world |
| 3 | A player the world HAS a character for (a restore is queued) — nothing granted, no second account, including when the restore lands between the body's entry and the grant | machine + partial | **FAIL** | the first guest's own log shows the grant being announced **before** its stored character arrived, five times: `Starting supplies for a new player: … starting supplies (light) given — emergencylight` at 11:46:09.787, 11:49:08.848, 11:51:28.566, 11:55:57.568 and 11:56:58.043, each followed 38–338 ms later by `Received character restore …` (`o-guest1-supplies-race.txt`) — the player DID have a character: its tree kept the same ids across every restore (`o-guest-local-tree-after-restore1/2/3.json`) and the host's binder bound it from the archive (`o-host-log-excerpt.txt`). The opposite order exists in the same run and takes the correct branch (`o-alt-supplies-decision.txt`: `Character restore pending for this body — the world has a character for this player, so no starting supplies`, 238 ms before its restore), so the outcome is ordering-dependent; the protection the ticket landed (check the queue at the grant moment) cannot see a character that has not arrived yet, and the granted item is then superseded — the exact "created, announced and then destroyed" hazard its review named |
| 4 | A FRESH run's first layer | machine | **pass** | all three clients read `CUO new player: the world's own first-layer supplies (light) are already yours — CUO granted nothing on top.` at run start (`o-host-console-baseline.json`, `o-guest-console-baseline.json`, `o-alt-console-baseline.json`); `StartingSupplyCoordinatorTests.Update_AFreshRun_ReportsAlreadyOwnedInsteadOfGranting` and `CommandConsoleSaveTests.StartingSupplies_OfAPlayerTheGameAlreadySupplied_SaysSoInsteadOfGranting` are green in `o-s4-suite.txt` |
| 5 | A RESTORED world frozen on its starting layer | machine | **pass** | `StartingSupplyCoordinatorTests.Update_ARestoredStartingLayer_DoesNotGrantASecondSet` and `StartingSupplyPolicyTests.NativeGrantCovers_ARestoredStartingLayer_IsStillTrue` in `o-s4-suite.txt` |
| 6 | The pump runs many frames over one body / a layer descent | machine | **pass** | `StartingSupplyCoordinatorTests.Update_TheSameBodyIsJudgedOnce_HoweverManyFramesRun`, `StartingSupplyGrantTrackerTests.WasSupplied_TracksEachBodyOnItsOwn`, `.WasSupplied_TwoSeparateInstances_AreTwoBodies` in `o-s4-suite.txt` |
| 7 | A new body after the old one is gone / a new run / a session end | machine | **pass** | `StartingSupplyCoordinatorTests.Update_ANewBodyAfterTheOldOneIsGone_IsJudgedOnItsOwnEntry`, `.Clear_ForgetsTheBodiesOfTheRunBeingLeft`, `StartingSupplyGrantTrackerTests.Clear_ForgetsEveryBody` in `o-s4-suite.txt` |
| 8 | The run's setting is `none`, or the run carries no settings | machine | **pass** | `StartingSupplyCoordinatorTests.Update_ARunWithNoSupplies_ReportsDisabledAndGrantsNothing`, `StartingSupplyPolicyTests.Decide_ARunWithoutRunSettings_ReportsDisabledUnset`, `.Decide_ADisabledRunOnTheFirstLayer_IsDisabledNotAlreadyOwned`, `CommandConsoleSaveTests.StartingSupplies_OfARunWithNoSupplies_SaysTheRunDecidedIt` in `o-s4-suite.txt` |
| 9 | The baseline is not published yet | machine | **pass** | `StartingSupplyCoordinatorTests.Update_WithNoPublishedBaseline_JudgesNothingAndRetries`, `StartingSupplyPolicyTests.Decide_NoBaseline_IsNotAVerdict` in `o-s4-suite.txt` |
| 10 | A slot the body cannot take / an id that produces no item | machine | **unproven** | the run did not stage an unplaceable item; `StartingSupplyCoordinatorTests.Update_AnItemTheGameCannotPlace_IsNamedInsteadOfClaimed` and `.Update_AContentIdThatProducesNoItem_IsNamedInsteadOfClaimed` are green in `o-s4-suite.txt`, but the player-visible partial-grant line was not observed |
| 11 | The setting's items and slots | machine | **pass** | `StartingSupplyPolicyTests.PlanFor_Full_IsTheGamesFourItemsInTheGamesSlots`, `.PlanFor_Light_IsTheEmergencyLightInTheGamesSlot`, `.PlanFor_AnUnknownValue_GrantsNothing` in `o-s4-suite.txt` |
| 12 | The account reaches the console in the production composition | machine | **pass** | `WorldSaveCompositionTests.ProductionRoot_WiresTheStartingSuppliesAccountToTheConsole`, `.ProductionRoot_ResolvesTheStartingSupplyAuditForTheAdapter` in `o-s4-suite.txt` |
| 13 | A run started from the debug console | machine | **pass** | `StartingSupplyPolicyTests.NativeGrantCovers_ARunStartedAtADebugDepth_IsFalse` in `o-s4-suite.txt` |
| 14 | The tutorial | machine | **pass** | `StartingSupplyPolicyTests.NativeGrantCovers_TheTutorial_IsFalse`, `.Decide_ARunWithoutRunSettings_ReportsDisabledUnset` in `o-s4-suite.txt` |

## Residuals for the user

- Row 1: whether the new player's account line reads clearly on screen (the line's content is the
  machine half; no frame was captured).

## Limits

- Row 3's failure is the ordering "grant fires, then the stored character arrives". The same run also
  shows the opposite order taking the correct branch, so this is a race with a reachable correct path,
  not a constant failure; the failing order repeated five times in the session and is the ordinary
  in-session path (a member entering a restored world).
- The announcement's console visibility is judged from the coordinator's own log line, which the
  ticket says carries the same words as the console; the guest's console history was not read live.
- Row 10's setup (an unplaceable item or a content id that produces no item) was not staged.
