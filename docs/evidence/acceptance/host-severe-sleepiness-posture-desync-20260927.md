# Acceptance record — Host severe sleepiness posture not synced to guest

- Ticket: `host-severe-sleepiness-posture-desync` — verdict: **moved to `done/`** (no unproven row; the
  visual half is a residual for the user)
- Batch: `20260927-d` — carry/pose family (see `20260927-d-scope.md`)
- Commit: `72fdb446` · Deployed artifact: `CasualtiesUnknownOnline.dll`, ProductVersion
  `+72fdb446446d34425db2ee9470152e3c3b0d3d8c` (deploy and `verify-deploy.ps1` exit 0)
- Run: 2026-09-27 23:58 → 2026-09-28 00:23 (+08:00) · Host: physical machine · Guest: Sandboxie
- Artifacts: `.acceptance/batch-d/` (`s1-s3` host readings; `f8/f9` frames)

| # | Row | Class | Verdict | Evidence |
|---|---|---|---|---|
| 1 | With severe sleepiness the host's own posture is slouched, not standing straight | visual | **residual** | Forced `energy=0 badSleepAmount=150 consciousness=31` (`s1`); the owner's `legSpeedMult` fell to `0.275` (`s2`), which is the game's own slouch input — but the world frame (`f8`) renders the body too small to read the pose |
| 2 | The guest's view of the host matches (no standing-straight proxy) | machine | **pass** | The guest-side host clone read `legSpeedMult=0.275` (`s3`), identical to the owner's value: the snapshot carries the pose input and the clone reconstructs the same crouch input |
| 3 | The pose input travels as the owner's `legSpeedMult` | machine | **pass** | `s2` owner `0.275` = `s3` clone `0.275`; before the change the clone had no owner value to read |

## Residuals for the user
- Row 1: look at the host's body on a full-size screen after forcing severe sleepiness — the machine
  reading says the slouch input is active, the picture is yours to confirm. Frame artifact `f8`
  (`.acceptance/batch-d/`).

## Limits
- The slouch is judged through the game's own pose input (`legSpeedMult` → `CrouchAmount`) at both
  ends; the render itself was not measurable at this zoom.
