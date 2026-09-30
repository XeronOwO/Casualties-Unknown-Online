# Acceptance record — A carried rider's own body stops simulating (flat ECG, twitching limbs)

- Ticket: `carried-rider-own-body-stops-simulating` — verdict: moved to `done/`
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
| 1 | Guest carries the host; the host's own medical panel shows the real heart rate and a normal ECG | visual | pass (batch `20260927-d`) | `carried-rider-own-body-stops-simulating-20260927.md` |
| 2 | Host carries the guest; the guest's own panel shows the same | visual | pass (batch `20260927-d`) | previous record |
| 3 | The rider's own screen shows normal limb animation, no twitching, no frequency growth | visual | residual (batch `20260927-d`) | previous record |
| 4 | The rider stays attached while the carrier moves, turns and crouches | machine + visual | pass (batch `20260927-d`) | previous record |
| 5 | Breathing, moodles, facial state and the other per-frame values advance on the rider's own client | machine | pass (batch `20260927-d`) | previous record |
| 6 | Release restores exactly the pre-carry state | machine | pass (batch `20260927-d`) | previous record; this run released both directions cleanly (`c1`, `d1`) |
| 7 | A third peer watching the carry is unchanged | visual | pass | The third client's clone state across both directions: `isCarrier`/`isCarriedRider` mirrored from the relation, `pinned-to-carrier` in 264/264 diagnostic windows, `limbSeparation=0` in every window, no `no pin` warning (`logs/carry-evidence-excerpt.txt`); frames `f2-third-a1-world.png`, `f4-third-a2-carrier-sit.png`; participant views unchanged (host 112/112, guest 153/153 zero-drift windows, no warnings) |
| 8 | The suppression family re-evaluated against the new mechanism | review | pass (cycle record + batch `20260927-d`) | the ticket's suppression table and the sibling records in this batch |

## Residuals for the user

- The third-party frame's pixel-level pose (a few pixels at the game's native zoom); the clone readings
  and the unchanged participant views are the deciding evidence.

## Limits

- "Unchanged" is judged on the third-party surface this run can read — mirrored carry role, continuous
  pins, zero limb separation, no anomaly warnings, and the frames. A per-pixel comparison against a
  two-client run is not part of that evidence.
- The third-party interpolation remainder measured in the sibling smoothing ticket's record applies to
  this view too; it is not a change caused by this ticket's mechanism.
