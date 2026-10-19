# Acceptance record — fentanyl / remote medical panel desync (the audit's surface symptom)

- Ticket: `remote-fentanyl-injection-and-medical-panel-desync` — verdict: **moved to `done/`** (every acceptance criterion passes; two follow-up findings are separate tickets)
- Batch: `20261023-b` — siblings `remote-medical-stage-1-injection-session-20261023-b.md`, `remote-medical-panel-acceptance-issues-20261023-b.md`
- Commit: `d960f05f` · Deployed artifact: `CasualtiesUnknownOnline.dll`, `0.1.0+d960f05f885ba255f656607f1905a2ad3b922167`
- Run: 2026-10-19 21:38 → 22:01 · three clients (host physical, guest sandbox 1, third sandbox 2)
- Artifacts: `.acceptance/20261023-b/`, cited below

| # | Criterion | Class | Verdict | Evidence |
|---|---|---|---|---|
| 1 | A guest using fentanyl on the host follows the native injection interaction, not an instant full drain | machine | **pass** | The remote release started the native `SyringeMinigame` (`syringe-drive` read `minigame=SyringeMinigame`, `i7-dose-1.json`), and the item drained in accepted 5 ml increments (condition 0.8506/0.8006/0.7506/0.7006) with one terminal — not one full drain at the end (`i7-item-*.json`, `z-host-latest.log` ops 3-5) |
| 2 | After the injection completes, the guest's panel shows the host's happiness rise to the expected post-fentanyl value | machine + visual | **pass** | op 4: host local opiate 73.8→115.0, opiateHappiness 52.4→94.6; operator display 73.9→115.1 and 49.0→92.2, moodles `overdose2/happy6` → `overdose3/happy7` (`i7-*`). The panel's own rows are judged in the sibling record row 1 |
| 3 | The remote panel shows a non-zero host heart rate and its ECG follows the same display body | machine + visual | **pass** | Panel read: display `heartRate=75.816` at baseline, `ecgDisplay=1/2` (panel ECG on the display body, consciousness overlay local); forced `heartRate=0` → display 0, `heartProg` delta 0 over 2 s, flat waveform in `p4b-guest-panel-heart-stop.png`; restored, display 81.067 (`p1b-*`, `p4b-*`, `p5b-*`) |
| 4 | While viewing another player, the bottom status icons show the viewed player's stats | machine + visual | **pass** | The guest's display moodles equal the host's own set in both paired states: `sick0` present/absent (`p6b-*`, `p7b-*`) and the fentanyl set `[hypotension0,hypoventilation1,overdose3,confused0,hunger0,thirst0,hot0,happy7,wet0]` (`i7-guest-panel-after.json`) |
| 5 | While viewing another player, the sleep control is disabled/blocked and no nap starts | machine | **pass** | While the remote focus was open the native nap control was not presented at all (`napVisible=false`, `napInteractable=false`, `p1b-guest-panel-baseline.json`); the workout list hidden and the HUD hand-swap graphic disabled (`workoutVisible=false`, `handSwapEnabled=false`); close restores them (`x3-*`, `p1b-*`). This is the newer 2026-09-21 ruling (hide, not disable) |
| 6 | All roles/directions are covered: guest→host, host→guest, third-party | machine | **pass** | guest→host op 4 (`i7-*`), host→guest op 6 (`i9-*`), guest→guest relay op 7 (`i10-*`), bystander view op 5 (`i8-*`) — each with the same committed-progress and terminal shape |
| 7 | Full build, tests and gates pass before review; the artifact is deployed and verified | machine | **pass** (batch scope) | This batch built the tree (`build.log`, 0 warnings/0 errors), deployed and verified `0.1.0+d960f05f…` (`deploy.log`, `verify.log`); the batch's focused medical-session suite ran green (9/9, `z-focused-tests.log`). The ticket's landing cycle recorded the full-suite/gates evidence; this run proves the deployed artifact is the tree's build |

## Residuals for the user

None for this ticket.

## Limits

- Two panel-level findings raised by the user during this run are outside this ticket's criteria and
  were filed separately:
  `docs/backlog/todo/remote-medical-panel-header-shows-viewer-identity.md` (the identity header keeps the
  viewer's data) and `docs/backlog/review/remote-medical-panel-alarm-follows-viewed-player.md` (the native
  monitor alarm follows the viewer's own body).
- The dose was driven through the native minigame's own `OnUse` delegate in process (declared in the
  stage-1 record); the pointer drag itself is not exercised.
