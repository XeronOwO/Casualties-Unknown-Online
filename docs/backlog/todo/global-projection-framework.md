# Global unified projection framework

- Status: Todo
- Priority: High
- Category: Architecture / projection framework
- Source: User direction (2026-09-07) — the earlier unified remote display projection work was only one projection domain; a macro/global projection system is required so items, players, world entities, fluids, enemies and remote presentation all converge on the same contract.

## Why this exists

CUO already has many projections:

- Items: `ItemProjection`, `KernelBatchItemProjection`, `WorldItemTable`, `RemoteInventorySnapshot`.
- Fluids: `FluidKernelProjection`, `FluidKernelReadProjection`.
- World entities: `WorldEntityKernelProjection`.
- Players: `PlayerKernelStatusProjection`, `PlayerKernelLimbProjection`, `PlayerKernelRestoreProjection`, `PlayerKernelCarryProjection`, remote character display.
- Enemies: `EnemyKernelProjection`, `EnemyKernelRestoreProjection`, `EnemyCombatKernelProjection`.

They share the same need — a rebuildable read model with health tracking,
diagnostics and a main-thread rebuild path — but only three domains were wired
into `ProjectionHealthCoordinator`, and each domain hand-rolled its own
dirty/rebuild/degraded story.

## Landed in this cycle

- `IProjectionDomain`: domain-neutral projection contract (`Domain`,
  `CurrentRevision`, `Rebuild`).
- `ProjectionDomain`: adapter from delegate registration to the typed contract.
- `ProjectionHealthCoordinator.Register(IProjectionDomain)`.
- The existing health-tracked domains (`items`, `fluids`, `world-entities`) now
  register through typed `ProjectionDomain`.
- `RemoteCharacterPresentation`: a concrete typed read model for remote player
  presentation (face, body pose, medical derived values, inventory view).
- Architecture document: `docs/architecture/projection-framework.md`.

## Remaining scope

1. Wrap every remaining projection domain in `IProjectionDomain` where a real
   rebuild path exists:
   - player terminal/limb/restore/carry projections;
   - enemy kernel/restore/combat projections;
   - remote character display (adapter-side surface);
   - world/run mapping and mod-status projections.
2. Add stable revisions/rebuild entry points for domains that are currently
   event-driven or delegate-only.
3. Make `ProjectionHealthCoordinator.Snapshot()` the single observability
   surface for the complete projection inventory.
4. Add regression/rebuild tests for every registered domain.
5. Keep the existing guarantee: projections never mutate authority and are
   rebuildable from the authoritative source.

## Acceptance criteria

- Every projection domain in CUO registers through the global contract.
- A single `Snapshot()` lists all domains with revision/dirty/degraded/error.
- A domain failure is contained and rebuilt on the main-thread pump.
- No new projection domain can be added without a typed read model and
  registered rebuild path.
- Full build, format, normative gates, full tests and independent adversarial
  review pass before moving to review.
