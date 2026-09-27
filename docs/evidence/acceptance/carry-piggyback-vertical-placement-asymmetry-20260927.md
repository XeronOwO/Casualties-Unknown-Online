# Acceptance record — Carry/piggyback vertical placement asymmetry

- Ticket: `carry-piggyback-vertical-placement-asymmetry` — verdict: **moved to `done/`**
- Batch: `20260927-d` — carry/pose family (see `20260927-d-scope.md`)
- Commit: `72fdb446` · Deployed artifact: `CasualtiesUnknownOnline.dll`, ProductVersion
  `+72fdb446446d34425db2ee9470152e3c3b0d3d8c` (deploy and `verify-deploy.ps1` exit 0; build 0
  warnings / 0 errors; gates 293/293; main suite 4520/4520)
- Run: 2026-09-27 23:58 → 2026-09-28 00:23 (+08:00) · Host: physical machine · Guest: Sandboxie
- Artifacts: `.acceptance/batch-d/` (`a5-a6`, `u7`, `w4`)

| # | Row | Class | Verdict | Evidence |
|---|---|---|---|---|
| 1 | Host-on-guest and guest-on-host show the same rider/carrier vertical relationship | machine | **pass** | Both directions read the rider at carrier + `(0.35, 0.9)`: `u7` (host rider at `40.947,410.515` vs carrier `40.597,409.615`) and `w4` (guest rider at `44.435,400.515` vs carrier `44.085,399.615`) |
| 2 | Correct while standing and while crouching | machine | **pass** | Standing offset `(0.35, 0.9)` (`u7`, `w4`); crouched offset `(0.35, 0.5)` with both bodies `crouching=true` (`a5/a6`: carrier `13.085,439.616`, rider `13.435,440.116`) — the vertical offset follows the crouch state the same way on both sides |
| 3 | Verified from both participants' perspectives | machine | **pass** | Each direction was read from both clients (`u6/u7`, `w3/w4`), and the two readings agree; the frames (`f1`) are too small to read a pose, noted under Limits |
| 4 | Existing carry/release/UI tests and repo gates stay green | machine | **pass** | This run's build and suites: gates 293/293, main 4520/4520, `build-final.log` / `full-tests-final.log` |

## Residuals for the user
- The "looks right on screen" half of rows 1-2: the world frames at this zoom do not resolve the
  bodies (`f1-zoom*`), so the perceived placement is left to a person. The measured offsets above are
  the machine half of the same claim.

## Limits
- The crouched reading (`a5/a6`) came from the same session before the ground-overlap environment
  finding was fixed; it is a placement reading, not a movement reading, and the crouch state is
  evident in the readings themselves.
