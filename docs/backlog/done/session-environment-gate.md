# A gate that keeps a run off a machine its owner is playing on

- Status: Done
- Acceptance (20261001-t): the nine black-box tests pass, the real install reads `active=cuo` and `ensure-cuo` is a no-op (row 3), and row 2's real-install swap stays the declared residual — record `docs/evidence/acceptance/session-environment-gate-20261001.md`.
- Priority: High
- Category: Acceptance tooling / machine safety
- Source: User request (2026-09-27): the run must detect whether they are playing before it touches the
  install — by the mod DLL inside each BepInEx tree, never by folder name — and must stop when a game is
  running rather than kill it or swap folders.
- Related: `docs/acceptance/workflow.md` (the run's step), `docs/acceptance/AGENTS.local.md` (the machine's
  marker and parking names), `tools/acceptance/session-environment.ps1`

## Problem

An install can carry two BepInEx trees: CUO's own, and the one the machine's owner keeps for their own
play, swapped by hand. A run that deploys or launches without asking writes into whichever tree is active,
and a run that "cleaned up" a running game would kill the owner's session with friends.

## Acceptance criteria

| # | Scenario | Expected |
|---|---|---|
| 1 | The owner's tree is active and the game is running | The gate reports `active=play`, `game-running=true`, `launch=blocked`, and `-Mode ensure-cuo` refuses (exit 2) without killing a process or moving a folder |
| 2 | The owner's tree is active and no game is running | `-Mode ensure-cuo` parks that tree and makes CUO's tree the active one |
| 3 | CUO's tree is already active | `-Mode ensure-cuo` reports `already-cuo` and moves nothing |
| 4 | No sibling tree carries the CUO marker, or the parking name is taken | The gate refuses with that reason and moves nothing |
| 5 | The play marker is not configured | The gate still classifies CUO's tree, reports the other one as `other`, and swaps safely |

## What landed

- `tools/acceptance/session-environment.ps1`: marker-DLL classification (`-Mode status`), a swap that runs
  only when no game process is running (`-Mode ensure-cuo`), exit 0/2/64, ASCII-only, bounded to `-GameDir`.
- Nine black-box tests (`SessionEnvironmentTests`) against staged fixtures: the report, the no-op, the swap,
  both refusals, the absent marker and the missing install.
- The run's step in `docs/acceptance/workflow.md`, the tool's index entries in `docs/acceptance/AGENTS.md`
  and `tools/AGENTS.md`, and the machine's marker/parking names in `docs/acceptance/AGENTS.local.md`.

## Limits

- **The swap path is verified on fixtures, not on the real install.** The machine was playing while this
  landed — exactly the state the gate refuses — so the first free moment re-runs `-Mode ensure-cuo` against
  the real install and closes this row.
- The gate is a step of the run, not a wrapper around `deploy.ps1`: deploy itself still refuses only on a
  running game. Folding the classification into `preflight.ps1` as a `session` row is the natural next
  increment if it should surface in every run automatically.
- **`deploy.ps1` and `verify-deploy.ps1` do not ask the gate.** With the owner's tree active they look for
  `<game-dir>\BepInEx\plugins\CasualtiesUnknownOnline`, so `verify-deploy.ps1` fails with "Run
  tools/deploy.ps1 first" while the run's own deployment sits intact in the parked tree (observed
  2026-09-27: that copy still carried `0.1.0+7dc9553d`). The step order covers it today; teaching those two
  scripts to consult the gate is the natural follow-up.
