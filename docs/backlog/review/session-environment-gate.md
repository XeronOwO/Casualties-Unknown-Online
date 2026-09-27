# A gate that keeps a run off a machine its owner is playing on

- Status: Review (code complete 2026-09-27: the tool, its nine black-box tests and the refusal path on the
  real machine are in; the real install's swap path still needs a moment when the machine is free — a
  residual, not a claim)
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
