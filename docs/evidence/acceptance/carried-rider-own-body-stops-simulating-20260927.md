# Acceptance record — A carried rider's own body stops simulating

- Ticket: `carried-rider-own-body-stops-simulating` — verdict: **stays in `review/`** (row 7 is split
  to a three-client run; row 3 is a residual)
- Batch: `20260927-d` — carry/pose family (see `20260927-d-scope.md`)
- Commit: `72fdb446` · Deployed artifact: `CasualtiesUnknownOnline.dll`, ProductVersion
  `+72fdb446446d34425db2ee9470152e3c3b0d3d8c` (deploy and `verify-deploy.ps1` exit 0)
- Run: 2026-09-27 23:58 → 2026-09-28 00:23 (+08:00) · Host: physical machine · Guest: Sandboxie
- Artifacts: `.acceptance/batch-d/` (`f4-f7`, `u6/u7`, `w3/w4`, `v1-v6`, `logs/*`)

| # | Row | Class | Verdict | Evidence |
|---|---|---|---|---|
| 1 | Guest carries host; the host opens its own medical panel → real heart rate and a normal ECG waveform | visual | **pass** | `f4`/`f5`: the host (rider) opened its own native `WoundView` in-process; both frames were read and show a clear QRS ECG that advanced between them (2 s apart) |
| 2 | Host carries guest; the guest opens its own panel → same, reverse | visual | **pass** | `f6`/`f7`: the guest (rider) opened its own panel; the waveform advanced between the two frames |
| 3 | The carried rider's own screen shows normal limb animation, no twitching and no growing frequency | visual | **residual** | The world frames do not resolve the limbs at this zoom; the trace's per-second fields (`standing`, velocity, heart progression) are logged at Debug and were not enabled this run. A person can confirm the limbs, or a later run can enable `Logging.MinimumLevel=Debug` and read the trace |
| 4 | The carrier moves/turns/crouches and the rider stays attached | machine | **pass** | Both directions, moving: `u6/u7` and `w3/w4` — `pin=129`/`pin=138`, `drift=0`, `limbSep=0`, `mounted=true`, rider at carrier + `(0.35, 0.9)` |
| 5 | Breathing, moodles, facial state and other per-frame values keep advancing on the rider's own client | machine | **pass** | Vitals advance while carried (`z2→z5→z7`: brainHealth/consciousness `1.004 → 1.011 → 1.033`); the attach/release log lines carry `heartRate` and `heartProg` (`logs/host-LogOutput.log`: attached `heartProg=0.955` → released `0.631`), i.e. the progression keeps moving under the carry |
| 6 | Release restores exactly the pre-carry state, immediately | machine | **pass** | `z8/z9` release: driver cleared, body back to its own state (`standing=false` ragdoll case); guest log `[Carry] rider simulation released … rbSimulated=True` |
| 7 | A third peer watching the carry is unchanged | visual | **unproven** | No third client this run; split to a three-client run |
| 8 | The suppression family is re-evaluated (each member kept or deleted with a reason) | machine | **pass** | The ticket's own re-evaluation table, plus this run's verification of the surviving members: rider idle-sit (`v1-v3`), carrier sit (`v4-v6`), seated transitions (`y`), vertical placement (`u/w`) |

## Residuals for the user
- Row 3: watch the rider's own limbs while carried. A Debug-level trace can substitute if the user
  prefers a reading over a look.

## Limits
- Rows 1-2 are read from the native medical panel, which is a large UI and stays readable; the world
  frames are not, as noted.
- Row 7 is the reason the ticket stays open.
