# Global unified projection framework

- Status: In Progress
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
- Additional runtime read-model domains registered through the global contract:
  - `run` — `WorldService` rebuilds `WorldStartParams` from `QueryRun()`.
  - `players-carry` — `PlayerKernelCarryProjection` rebuilds the carry mirror
    from the kernel player table.
  - `remote-character-presentation` — `RemoteCharacterPresentationStore`
    deep-copies the latest character-data snapshots and rebuilds the typed
    presentation model as the registry-backed domain read model; the existing
    `RemoteVitalsService` / `RemoteInventoryService` remain separate session
    caches for their UI-specific snapshot shapes.
  - `mod-status` — `ModStatusProjectionReadModel` registers the local
    mod-status projection domain: the store now has a monotonic revision, the
    read model rebuilds projection snapshots/status presences from the store,
    and the GameAdapter vanilla body/limb + moodle projections consume that
    read model.
- Boundary audit completed for enemy/player continuous runtime projections and
  remote-presentation read-source unification; see
  `docs/architecture/projection-framework.md`.
- Regression/rebuild coverage for these domains; architecture document updated:
  `docs/architecture/projection-framework.md`.

## Verification status

- `dotnet build` and `dotnet format` pass.
- Full test suite: 2516 `CasualtiesUnknownOnline.Tests` + 17 normative gates pass.
- Two independent adversarial reviews were run; the host-authoritative presence
  leak found in the second review was fixed and covered by a regression test.
- Deployed to the entity game directory and build/deploy SHA256 comparison passed.
- This ticket intentionally remains `in-progress/` per the global-framework
  staging discipline; it is not yet claimed complete or moved to `review/`.

## Remaining scope / audited boundary

1. The named non-rebuildable projection classes remain intentionally outside
   the global contract. They are write-side adapters into kernel authority,
   stateless restore appliers, or one-shot event fan-outs:
   - `PlayerKernelStatusProjection`, `PlayerKernelLimbProjection`,
     `PlayerKernelRestoreProjection`, `PlayerInteractionKernelProjection`;
   - `EnemyKernelProjection`, `EnemyKernelRestoreProjection`,
     `EnemyCombatKernelProjection`, `FluidKernelProjection` (host-side input
     path, while `FluidKernelReadProjection` is already registered).
2. Mod-status local projection is implemented through `mod-status` in the
   inventory. Enemy/player continuous runtime full-read projections were
   audited and confirmed non-rebuildable (documented in the architecture doc),
   so they are not new domains. Remote presentation store unification was
   evaluated and deliberately deferred: the store only tracks full snapshots,
   while the live adapter display cache is event-enriched; unification needs
   first-class event projection plus dual-client runtime verification.
3. Keep `ProjectionHealthCoordinator.Snapshot()` as the single observability
   surface for every registered projection domain.
4. Keep the existing guarantee: projections never mutate authority and are
   rebuildable from the authoritative source; no new projection domain may be
   added without a typed read model and registered rebuild path.

## Acceptance criteria

- Every rebuildable projection domain in CUO registers through the global
  contract; write-side/native-to-kernel adapters, stateless restore appliers and
  one-shot event fan-outs are explicitly not projection read models and are not
  part of this inventory.
- A single `Snapshot()` lists all domains with revision/dirty/degraded/error.
- A domain failure is contained and rebuilt on the main-thread pump.
- No new projection domain can be added without a typed read model and
  registered rebuild path.
- Full build, format, normative gates, full tests and independent adversarial
  review pass before moving to review.
