# Acceptance record — Carry/piggyback riding movement teleport and rider/carrier position mismatch

- Ticket: `carry-piggyback-rider-position-smoothing` — verdict: **stays in `review/`** (row 3 is
  split to a three-client run; row 1's feel half is a residual)
- Batch: `20260927-d` — carry/pose family (see `20260927-d-scope.md`)
- Commit: `72fdb446` · Deployed artifact: `CasualtiesUnknownOnline.dll`, ProductVersion
  `+72fdb446446d34425db2ee9470152e3c3b0d3d8c` (deploy and `verify-deploy.ps1` exit 0; build 0/0;
  gates 293/293; main suite 4520/4520)
- Run: 2026-09-27 23:58 → 2026-09-28 00:23 (+08:00) · Host: physical machine · Guest: Sandboxie
- Artifacts: `.acceptance/batch-d/` (`u6/u7`, `w3/w4`, `f4-f7`, `logs/*`)

| # | Row | Class | Verdict | Evidence |
|---|---|---|---|---|
| 1 | Host-on-guest and guest-on-host movement shows no frame teleport on the participant views | machine + feel | **pass (machine) / residual (feel)** | Both directions ran a movement window: the carrier travelled (guest `36.607,414.616 → 40.597,409.615`; host `40.597,409.615 → 44.085,399.615`) and the clone readings stayed `drift=0` (`u6`, `w3`). Frame-level smoothness is a person's judgement (frames `f1` at this zoom cannot carry it) |
| 2 | The rider stays at the expected back offset through movement, facing and crouch changes | machine | **pass** | Moving: rider at carrier + `(0.35, 0.9)` (`u7`, `w4`). Crouched: `(0.35, 0.5)` (`a5/a6`). Facing was changed during the window; the pin readings stayed `drift=0`, `limbSep=0`, `mounted=true` |
| 3 | Any third-party view keeps the rider attached within normal smoothing tolerance | visual | **unproven** | No third client this run; split to a three-client run |
| 4 | The conscious/alive local rider's limbs render consistently with the remote rider clone | visual | **residual** | The clone/live limb rendering is not readable from the world frames at this zoom; the state reads (`limbSep=0` for the limp case in `z10`) are the machine half |
| 5 | No change to carry authority or the carry relation invariants | machine | **pass** | No carry code changed in this batch; relation lifecycle exercised repeatedly (`u`, `w`, `y`, `z` series) and every attach/release is in the logs with the expected mode |
| 6 | Existing carry/release/UI tests, build, format and repo gates pass | machine | **pass** | This run: build 0 warnings / 0 errors; gates `293/293`; main suite `4520/4520` (`build-final.log`, `full-tests-final.log`) |
| 7 | Regression coverage includes the carry visual-standing rule and the shared placement path | machine | **pass** | Those cases are inside the green suites above (`CarriedRiderMountTests`, `CarriedLimbAnchorTests`, `CarriedBodyPoseTests`) |

## Residuals for the user
- Row 1's smoothness (no visible jump) and row 4's limb rendering: run a carry on a full-size screen.
  The readings say the pin wrote every frame with zero drift; the picture is the residual.

## Limits
- The movement windows used the evaluator's slide mode (a real velocity write), because walking is
  input-frame-eaten in this session; displacement is real and measured, the input path is not a
  person's keyboard.
- The batch's own scope page records the ground-overlap environment finding that preceded the movement
  work; the readings above come after both bodies were freed.
