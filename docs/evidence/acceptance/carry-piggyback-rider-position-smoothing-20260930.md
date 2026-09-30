# Acceptance record — Carry/piggyback riding movement teleport and rider/carrier position mismatch

- Ticket: `carry-piggyback-rider-position-smoothing` — verdict: moved to `done/`
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
| 1 | Participant views show no frame teleport during movement | visual / feel | pass (batch `20260927-d`); the millisecond feel half stays a residual | `carry-piggyback-rider-position-smoothing-20260927.md` |
| 2 | The back offset holds through movement, facing and crouch | machine + visual | pass (batch `20260927-d`) | previous record |
| 3 | A third-party view keeps the rider attached within normal remote-player smoothing | visual / feel | pass | Third client's carry readings through the movement windows: Scene A1 pin `56`, `riderDrift=0`, `limbSeparation=0` (`a8`, frame `f2-third-a1-world.png` + `f2-third-a1-zoom.png`); Scene A2 pin `28` → `8`, `riderDrift=0`, `limbSeparation=0` (`c3`, `c5`); its own logs show 264 pinned windows, 260 exactly zero and 4 reportable (0.018–0.079 world units), while the participant views read host 112/112 and guest 153/153 zero-drift windows with no warnings (`logs/carry-evidence-excerpt.txt`) |
| 4 | The conscious rider's limbs render consistently with the remote clone | visual | residual (batch `20260927-d`) | previous record |
| 5 | No carry authority or relation-invariant change | declaration | pass (batch `20260927-d`) | previous record |
| 6 | Build, tests, format and repo gates pass | run | pass (batch `20260927-d`; this run's commit ran the same suites: 293/293 gates + 4524/4524 tests) | previous record, this batch's scope commit |
| 7 | Regression coverage includes the visual-standing rule and the shared placement path | run | pass (batch `20260927-d`) | previous record |

## Residuals for the user

- The third-party movement feel and the four reportable windows (0.018–0.079 world units ≈ up to 1 px
  at the game's native zoom, all in the reverse-direction burst): look at `f2-third-a1-world.png` and
  `f2-third-a1-zoom.png` for whether that reads as attached.

## Limits

- A third-party clone is deliberately not mounted: it uses the world-space pin plus the normal remote
  interpolation, and the drift reading is taken before the per-frame re-pin. A small non-zero reading on
  that view is the smoothing path this row names; the participant views read zero in every window.
- The reverse-direction burst was partially blocked by terrain (the carrier travelled 0.8 units across
  8 slide calls), so the four reportable windows are not a fast-travel sample.
- The frames cannot resolve ~1 px; the clone readings are the deciding evidence and the frames are the
  corroboration.
