# Acceptance record — Suppress native idle-sit while a player is being carried/piggybacked

- Ticket: `carried-player-idle-sit-suppression` — verdict: moved to `done/`
- Batch: `20260930-e` — tickets `carrier-sit-while-carrying`, `carry-piggyback-rider-position-smoothing`,
  `carried-player-idle-sit-suppression`, `carried-rider-own-body-stops-simulating` (group A, run 1)
- Commit: `751b6a91` · Deployed artifact: CasualtiesUnknownOnline.dll, ProductVersion
  `0.1.0+751b6a915b23a25c767b52529c0f0543e8df8032`
- Run: 2026-09-30 20:08 → 20:18 (+08:00) · Host: physical machine · Guest: primary sandbox · Third
  client: alternate sandbox
- Dependencies: `steam`, `game`, `deploy`, `sandboxie`, `sandbox-alt`, `hotrepl`, `dotnet`, `capture`,
  `input`, `logs`, `artifacts` (preflight 11 present, exit 0)
- Artifacts: `batch-e` (frames, probes, logs) in the directory named by `acceptance-artifacts-dir`

| # | Row | Class | Verdict | Evidence |
|---|---|---|---|---|
| 1 | The rider's own client never publishes `Sitting=true` while carried | machine | pass (batch `20260927-d`) | `carried-player-idle-sit-suppression-20260927.md` |
| 2 | The carrier-side rider clone never replays native sit clips | visual | residual (batch `20260927-d`) | previous record; this run's carrier-clone reads are in the sibling carrier-sit record |
| 3 | A body already in `ExperimentSit`/`ArmsSit` when carry begins returns to the ride presentation | machine + visual | pass (batch `20260927-d`) | previous record |
| 4 | The carried-ride `idleTime` is held at zero every frame | machine | pass (batch `20260927-d`) | previous record |
| 5 | The general remote-clone sit-end transition restores `Grounded` | machine | pass | A stationary non-carried body (the third client's own body) sat natively while the host watched its clone; the owner then moved and the clone left the sit state: before `s1-host-remote-sit-before.json` (both remote clones `ExperimentSit`/`ArmsSit`, `prevSitting=true`), owner `s2-third-body-read-after-stand.json` (`clip=ExperimentRunBack`, `idleTime=0`), after `s3-host-remote-sit-after.json` (the owner's clone `bodyClip=ExperimentWallBackward`, `armsClip=ArmsAirDown`, `idleTime=0`, `prevSitting=false`, at the owner's position), while the control clone stayed `ExperimentSit`/`ArmsSit`, `prevSitting=true` |

## Residuals for the user

- None for this row; the sit-start/sit-end states are read from the watcher's clone, which is the state
  that view renders.

## Limits

- One transition was observed in this session. The literal `Grounded` clip is the transition's first
  frame; the owner's own locomotion clip takes over within the same 1 Hz window, so the observable is
  "the sit clip is gone and the normal presentation returned", not that clip name alone.
- The clone's `idleTime` follows the stream, not a local simulation; the row's claim is about the clone
  presentation, and that is what was read.
