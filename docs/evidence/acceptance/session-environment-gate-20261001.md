# Acceptance record — A gate that keeps a run off a machine its owner is playing on

- Ticket: `session-environment-gate` — verdict: moved to `done/` (the real-install swap half is the
  ticket's declared residual, recorded below)
- Batch: `20261001-t` — offline batch; scope and limits: `docs/evidence/acceptance/20261001-t-scope.md`
- Commit: the cycle's commit (this record is committed with it); the run was performed on the working
  tree over `15ffc42a`
- Deployed artifact: `CasualtiesUnknownOnline.dll` `0.1.0+1b088511d555cf09ca19b336c2f3715f94c7df4e` —
  `tools/verify-deploy.ps1` exit 0, "Deployment matches this tree's build output"; no client was started
- Run: 2026-10-01 — preflight 17:46 (11 present, exit 0); focused evidence run 17:47 (the nine
  `SessionEnvironmentTests`, all passed); the tool's live read of the real install at 17:52; full suite
  with build (4,573 + 300, 0 failed)
- Dependencies: `dotnet` (both suites), the tool's own live read (`session-environment.ps1 -Mode
  status` / `-Mode ensure-cuo`), repository inspection; no game client
- Artifacts: the `20261001-t` directory under `acceptance-artifacts-dir` —
  `session-environment-status.txt`, `session-environment-ensure-cuo.txt`, `focused-20261001-t.log`,
  `full-suite-20261001-t.log`

| # | Row | Class | Verdict | Evidence |
|---|---|---|---|---|
| 1 | The owner's tree is active and the game is running: report `active=play`, `game-running=true`, `launch=blocked`; `ensure-cuo` refuses (exit 2) without killing a process or moving a folder | machine | pass | `Status_ReportsTheOwnerPlaying`, `EnsureCuo_RefusesWhileAGameProcessIsRunning` and `Status_LeavesTheTreesAlone` `Passed` (staged fixtures; the running game is the test's own PowerShell process in the process-name role, so the branch is deterministic) |
| 2 | The owner's tree is active and no game is running: `ensure-cuo` parks that tree and makes CUO's tree active | machine | pass on fixtures; **residual on the real install** | `EnsureCuo_SwapsWhenNoGameProcessIsRunning` `Passed` (`action=swapped`, the parked tree keeps its marker, no copy left). The real install is already CUO's tree, and this batch does not swap the owner's trees, so the first genuinely play-owned moment still owns this half — the ticket's own declared residual |
| 3 | CUO's tree is already active: `ensure-cuo` reports `already-cuo` and moves nothing | machine | pass (live) | this run against the real install: `-Mode status` printed `active=cuo`, `game-running=false`, `swap-needed=false`, `launch=ok`; `-Mode ensure-cuo` printed `action=already-cuo`, exit 0; the read-back status was unchanged; `EnsureCuo_IsANoOpWhenCuoIsAlreadyActive` `Passed` |
| 4 | No sibling tree carries the CUO marker, or the parking name is taken: the gate refuses with that reason and moves nothing | machine | pass | `EnsureCuo_RefusesWhenNoCuoTreeExists` `Passed` (exit 2, `reason=cuo-tree-count=0`, the other tree's marker untouched); the parking-name half is decided by source: the `reason=parked-name-taken=$ParkedTreeName` branch runs when the swap is needed and the parking name is occupied, and the refusal path performs no swap |
| 5 | The play marker is not configured: the gate still classifies CUO's tree, reports the other as `other`, and swaps safely | machine | pass | `EnsureCuo_SwapsWithoutThePlayMarkerConfigured` and `Status_NamesNoTreeWithoutItsMarker` `Passed` |
| 6 | The tool contract: marker-DLL classification, a swap only while no game runs, exit codes 0/2/64, ASCII-only, bounded to `-GameDir` | machine | pass | the nine `SessionEnvironmentTests` `Passed`; `Usage_WhenTheGameDirDoesNotResolve` asserts exit 64; the script reads 6,450 bytes with 0 non-ASCII bytes; this run's live `status`/`ensure-cuo` outputs match the contract |

## Residuals for the user

- Row 2's real-install half: when the machine is next free with the play tree active, run
  `tools/acceptance/session-environment.ps1 -Mode ensure-cuo` and confirm the swap against the real
  install. Recorded here as the ticket's declared residual, not a claim of this batch.

## Limits

- No game was started; "the game is running" is the black-box test's own deterministic process-name
  stand-in, and the real install was only read (`-Mode status`, and a no-op `ensure-cuo`).
- Row 4's parking-name half is decided by reading the script's own refusal branch, not by an executed
  scenario; the executed half is the no-CUO-tree refusal.
