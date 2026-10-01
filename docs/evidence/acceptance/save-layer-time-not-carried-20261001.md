# Acceptance record — layer time accounting (resume ruling)

- Ticket: `save-layer-time-not-carried` — verdict: **moved back to `todo/`** as `- Status: Todo — Rejected
  (row 1: the layer timer did not resume in the live game)`; the rejection loop is ordinary development work
- Batch: `20261001-m` (Run A) — tickets `save-layer-end-save-and-restore`, `save-mid-run-consistent-cut`,
  `save-native-run-field-parity`, `save-native-character-field-parity`, `save-layer-time-not-carried`,
  `world-determinism-world-fingerprint`
- Commit: `e86241a5` · Deployed artifact: `CasualtiesUnknownOnline.dll`, ProductVersion `0.1.0+e86241a5`
- Run: 2026-10-01 10:34 → 10:47 · Host: physical machine (Steam) · Guest: sandbox
- Dependencies: `steam`, `game`, `deploy`, `sandboxie`, `hotrepl`, `input`, `capture`, `logs`, `artifacts`
- Artifacts: the ids below, in the directory named by `acceptance-artifacts-dir`

| # | Row | Class | Verdict | Evidence |
|---|---|---|---|---|
| 1 | The timer resumes at 6 of 10 minutes | machine | **fail** | the forced precondition was written and archived — `layer-time-set seconds=360` reports `before 126.73 → after 360` (`run-a/layer-time-set-360.json`), the cut that follows carries `layerTimeSpent 373.9` (decoder line in `run-a/host-restore.txt`) and the applier logs "the layer timer 373.9s (written)" (`run-a/host-cuts.txt`); the live value right after the Continue is `0.3999` and then accumulates from zero (`run-a/timer-after-continue-host.json`: `0.3999828`; later reads `44.3`), so the timer restarts instead of resuming. The same shape repeated in the earlier cycle (handover "14.0s (written)", live `0.012`) |
| 2 | An archive without the row restarts the timer and says why | machine | **unproven** | not staged: the run never continued from a layer-end cut (the archive without the `layerTimeSpent` property), so the "restart + named absence" behaviour was not observed |
| 3 | A layer advance keeps the game's accounting | machine | pass | after the restore, `game-console skiplayer` to the next layer restarts the timer and keeps the game's own limit: `layerTimeSpent 0.0121`, `maxTimePerLayer 3600` on the host, `1.23` on the guest (`run-a/rarity-layer4-host.json`, `run-a/rarity-layer4-guest.json`) |

## Residuals for the user

- None; the failing row is a machine reading.

## Limits

- The run reproduced the restart twice (two independent cut/continue cycles), so it is not a one-off from a
  single sample; it does not attempt the code fix, which is the rejection loop's work.
