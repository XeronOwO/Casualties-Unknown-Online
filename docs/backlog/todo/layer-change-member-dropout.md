# Consecutive layer changes drop the members out of the world and storm the log

- Status: Todo
- Priority: Medium
- Category: World generation / layer transition / observability
- Source: agent acceptance batch `20261005-b` (2026-10-05), observed while staging that run's row 4 and
  recorded there as a limit; promoted to development work by the user's 2026-10-06 ruling that "not
  introduced by this change" is never a reason to leave a defect alone.
- Related: `done/layer-mod-baseline-divergence-on-continue.md` (the same warning from another producer),
  `done/guest-generation-segments-over-host-absence.md`, `done/reenter-baseline-adoption.md`,
  `review/steam-transport-send-limit-runaway.md` (the sibling unbounded-warning family)

## Symptom (evidence)

Batch `20261005-b` needed a layer change to empty the host's world-item table (that row's shape). Its own
record (`docs/evidence/acceptance/guest-command-loss-reconciliation-20261005-b.md`) states that after the
first staging attempt's layer change the two members were out of the world with a repeating
`[LayerMod] baseline divergence` warning storm: the guest's rolling log grew from 0.8 MB to 33.4 MB in
about four minutes. Nothing recovered on its own — the batch restarted all three clients cold and judged
the remaining rows in a second session.

The same record's limits name the two facts that make this a mechanism rather than a one-off:

1. Each staging attempt costs a layer change, and **a second layer advance follows the first on its own
   about 9 s later** — so one command is already a consecutive change.
2. At +4.1 s after the first attempt's change the member had no local body at all
   (`container-read mode=local` → `no-local-body`), which is why its destroy probe found nothing to
   destroy.

The run's third attempt (`r4d-*`: one change with both ends' inbound parked for 9.7 s) succeeded, so the
hazard did not block that ticket's row; it cost the batch a session. Artifacts: `r4-*`, `r4b-*`, `r4c-*`,
`r4d-*` and the two sessions' log excerpts, in the directory named by `acceptance-artifacts-dir`.

## Ownership (checked 2026-10-06)

| Fact | Origin |
|---|---|
| The warning and the decision it reports | `LayerModifierSync` (the `[LayerMod] baseline divergence` line) landed with `63ee8b4a` ("the layer modifier is host-authoritative"; the member replays the host's decision) and was last changed by `32fd0ebd` ("rewind the layer-modifier decision to the segment start") — both this repository |
| The residual-modifier clear at a layer boundary | `WorldGeneration.world.ResetLayerModifiers()` with the `[LayerMod] non-none layer — cleared the previous layer's residual modifiers` line, landed with `c3c2df11` (the tutorial/debug layer-modifier residual fix) and moved by `0e0665a4` (the GameAdapter split) — this repository |
| The trigger | `skiplayer` is the game's own console command: the word appears nowhere under `src/`, and the run sends it through CUO's console action |

So the diagnostic, the host-authoritative modifier model and the segment/adoption behaviour around a layer
change are ours; only the command that performs the debug skip is native. That makes the hazard this
repository's to act on — an external observation is not the whole story.

## What is not known yet

1. Which way the causation runs: the members leaving the world may be a consequence of the divergence, its
   cause, or a third effect of a layer change the game's debug path performs outside the generation
   boundary CUO expects.
2. Whether an ordinary layer-by-layer descent (the elevator, not the debug skip) reproduces any of it.
3. Whether the warning is emitted once per periodic world-item snapshot with no bound at all: the growth
   figure suggests a per-cycle line for minutes, but the emission path has not been read yet.

## Required work

1. **Attribute it first**, with the red step this repository requires: a test or a runtime probe that shows
   the member's exit and the warning on the pre-fix tree.
2. **Bound the diagnostic.** A repeated divergence must not be able to grow a log without limit — one line
   per distinct baseline pair, or an escalation — while keeping the divergence observable, because
   `done/layer-mod-baseline-divergence-on-continue.md` shows it is a genuine detector.
3. **Recover the member.** A member that has left the world after a layer change re-baselines or re-enters
   without a cold restart of every client.
4. **Audit the family**: every other per-cycle warning in CUO gets the same bound check;
   `review/steam-transport-send-limit-runaway.md` is the known sibling and stays its own ticket.
5. **Real-machine acceptance** with three clients and a consecutive layer-change staging, its rows written
   before the run. Schedule that run LAST in its batch: the storm and the members' exit out of the world are
   the machine's own cost, and the staging procedure, the recovery shape and the 15-second black-window
   ceiling are machine facts of the acceptance area's gitignored local files, not of this ticket.

## Non-goals

- Removing or weakening the divergence detector: it caught a real defect once and stays.
- Guarding the debug skip command itself: it is the game's own console command.
