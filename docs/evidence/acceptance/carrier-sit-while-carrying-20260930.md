# Acceptance record — Carrier can sit while carrying a player

- Ticket: `carrier-sit-while-carrying` — verdict: moved to `done/`
- Batch: `20260930-e` — tickets `carrier-sit-while-carrying`, `carry-piggyback-rider-position-smoothing`,
  `carried-player-idle-sit-suppression`, `carried-rider-own-body-stops-simulating` (group A, run 1 of the
  batch's plan)
- Commit: `751b6a91` · Deployed artifact: CasualtiesUnknownOnline.dll, ProductVersion
  `0.1.0+751b6a915b23a25c767b52529c0f0543e8df8032`
- Run: 2026-09-30 20:08 → 20:18 (+08:00) · Host: physical machine · Guest: primary sandbox · Third
  client: alternate sandbox
- Dependencies: `steam`, `game`, `deploy`, `sandboxie`, `sandbox-alt`, `hotrepl`, `dotnet`, `capture`,
  `input`, `logs`, `artifacts` (preflight 11 present, exit 0)
- Artifacts: `batch-e` (frames, probes, logs) in the directory named by `acceptance-artifacts-dir`

| # | Row | Class | Verdict | Evidence |
|---|---|---|---|---|
| 1 | The carrier cannot sit while carrying, both directions | machine | pass (batch `20260927-d`) | `carrier-sit-while-carrying-20260927.md` |
| 2 | A carrier already sitting when the carry begins returns to the carry presentation | machine + visual | pass (batch `20260927-d`) | `carrier-sit-while-carrying-20260927.md` |
| 3 | The suppression is visible on all participating and third-party views | visual | pass | Third client's clone of the carrier, both directions, after the carrier's `idleTime` was forced to 13: `isCarrier=true`, `idleTime=0.006`, `bodyClip=ExperimentIdle`, `armsClip=ArmsIdle`, `prevSitting=false` (group A: `b4` guest carrier, `c8` host carrier); the watcher's frames `f3-third-a1-carrier-sit.png`, `f4-third-a2-carrier-sit.png`; participant reads `b2`/`c7` |
| 4 | Normal non-carried idle-sit is unchanged | machine + visual | pass (batch `20260927-d` + this run's control) | The watcher's other clone stayed `ExperimentSit`/`ArmsSit` with `prevSitting=true` through the same window (`s1`, `s3`); previous record |
| 5 | No carry authority, release semantics or wire change | declaration | pass (batch `20260927-d`) | `carrier-sit-while-carrying-20260927.md` |

## Residuals for the user

- The pose on the third-party frames (`f3`, `f4`) is a few pixels at the game's native zoom; look at the
  frames if the pixel-level pose matters. The deciding evidence is the watcher's clone state, which is
  the same state that view renders.

## Limits

- One forced instant per direction; the native idle timer is zeroed every frame for a carry participant,
  which is what the clone's `idleTime=0.006` and non-sit clip show.
- The third-party view was judged through the watcher's own clone state and frames, not through a person
  watching the screen.
