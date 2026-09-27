# Acceptance record — Suppress native idle-sit while a player is being carried/piggybacked

- Ticket: `carried-player-idle-sit-suppression` — verdict: **stays in `review/`** (row 5 unproven)
- Batch: `20260927-d` — carry/pose family (see `20260927-d-scope.md`)
- Commit: `72fdb446` · Deployed artifact: `CasualtiesUnknownOnline.dll`, ProductVersion
  `+72fdb446446d34425db2ee9470152e3c3b0d3d8c` (deploy and `verify-deploy.ps1` exit 0)
- Run: 2026-09-27 23:58 → 2026-09-28 00:23 (+08:00) · Host: physical machine · Guest: Sandboxie
- Artifacts: `.acceptance/batch-d/` (`s1-s3`, `v1-v3`, `y1-y2`)

| # | Row | Class | Verdict | Evidence |
|---|---|---|---|---|
| 1 | The rider's own client never publishes `Sitting=true` while carried | machine | **pass** | `v2/v3`: the rider's `idleTime` stayed `0.006` and its clip stayed `ExperimentAirUpForward` (ride pose, not a sit) |
| 2 | The carrier-side rider clone never replays the native sit clips | visual | **residual** | The clone's clip is not in the probe surface; the frame (`f1`) cannot resolve the pose at this zoom — a person should confirm the seated-looking clone never appears |
| 3 | A body already in `ExperimentSit`/`ArmsSit` when carry begins returns to the ride presentation | machine | **pass** | `y1/y2`: the guest was seated (`x3`: `clip=ExperimentSit`) and climbed onto the host; it then read `clip=ExperimentAirUpForward`, `idleTime=0.006` |
| 4 | The carried-ride `idleTime` is held at zero every frame | machine | **pass** | `v1` forced `idleTime=13`; `v2` (+0.5 s) and `v3` (+2.5 s) both read `0.006` |
| 5 | The general remote-clone sit-end transition now restores `Grounded` for every stationary remote proxy | machine | **unproven** | This run never put up a stationary, non-carried remote clone that had to end a sit; what would close it: a scenario where a peer sits and stands outside any carry relation, read from the other client |

## Limits
- Row 5 is the "fix the family" half of the ticket; the carried half is verified above, the general
  proxy half needs its own scenario and stays open.
