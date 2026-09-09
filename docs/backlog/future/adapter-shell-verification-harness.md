# Adapter-shell sync paths have no automated verification (game-in probe harness)

- Status: Future (deferred by user decision 2026-09-09)
- Priority: Low
- Category: Verification tooling
- Source: accumulated from `review/runtime-entity-spawn-backfill.md` and earlier deliveries; `AGENTS.local.md` "验证能力边界"
- Related: `review/runtime-entity-spawn-backfill.md`, `docs/evidence/test-parallelization.md`

## Deferral note

Deferred by decision: the current verification standard for adapter-shell paths is code
review plus the user's unified dual-client acceptance pass, and that is accepted. This item
is NOT a work item and is not included in handoff prompts; promote it to `todo/` only if
adapter-shell regressions keep reaching the user's acceptance step (a clear need appearing
is the promotion trigger).

## Problem (evidence)

A recurring class of adapter-shell behaviour cannot be exercised by the test host,
because it needs the live Unity world. Current examples (all named as "code-reviewed
only" in their tickets):

- the runtime-entity creation materialization, the `RuntimeEntityCreation` stamping and
  the death-hook key read (`src/CasualtiesUnknownOnline.GameAdapter/World/EntitySpawnSync.cs`),
- the deferred geyser report/apply queue (`EntitySpawnSync.FlushReports`/`FlushApplies`),
- the enemy runtime-spawn materializer
  (`src/CasualtiesUnknownOnline.GameAdapter/Character/EnemySyncCoordinator.RuntimeSpawns.cs`),
- the mod template materializer and its half-built-instance cleanup
  (`RuntimeEntityFactory.cs`, `UtilsCreateCustomPrefabPatch.cs`).

Those paths are covered by code review plus the unified dual-client acceptance pass only.
That is a documented, accepted boundary today — but it means a regression there is found by
the user, not by the gate, and several deliveries have carried it as their residual.

## Goal

Build the minimum game-in verification seam that turns "code-reviewed only" into
"machine-checked", e.g.:

- a probe that records entity identity / transform / health snapshots to a file for a
  scripted scenario, asserted by a test or a `tools/` script, and/or
- a dual-client automation harness (scripted input + screenshots) for the acceptance
  scenarios the user currently performs by hand.

## Acceptance

- At least the E3 adapter-shell paths are covered by an automated, repeatable check that
  fails on a regression (demonstrate the red).
- The check is runnable from `tools/` or a test trait and documented under `docs/evidence/`.
- Until it exists, every ticket that relies on adapter-shell paths must name the exact
  unverified branches and the acceptance step that covers them (E3 is the template).
