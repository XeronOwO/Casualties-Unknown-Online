# Unified Projection Framework

This document describes the global projection architecture of CUO, not only the
remote character display slice. It is the macro-level answer to "what is a
projection system" and how all domains should converge.

## 1. Definition

A **projection** is a rebuildable read model derived from an authoritative
source. It never mutates authority. Projections exist for gameplay state,
network caches, native Unity objects, remote clones, UI readouts, and saves.

The common contract is:

```text
Authoritative source  --(projection apply/rebuild)-->  typed read model  --(surface apply)-->  native/UI/remote view
```

## 2. Why a global framework

Before this framework each domain hand-rolled its own projection shape:

- Items have `ItemProjection` / `KernelBatchItemProjection` / `WorldItemTable`.
- Fluids have `FluidKernelProjection` / `FluidKernelReadProjection`.
- World entities have `WorldEntityKernelProjection`.
- Players have `PlayerKernelStatusProjection`, `PlayerKernelRestoreProjection`,
  `PlayerKernelCarryProjection`, and the remote character display path.
- Enemies have `EnemyKernelProjection`, `EnemyKernelRestoreProjection`,
  `EnemyCombatKernelProjection`.

These share the same fundamental needs but had no single surface to express
them: a stable domain name, a current authoritative revision, a rebuild entry
point, health tracking, dirty/degraded diagnostics, and a common failure
containment path.

## 3. Core abstractions

The global contract is now expressed by:

| Type | Purpose |
|---|---|
| `IProjectionDomain` | Domain-neutral projection contract: name, current revision, rebuild. |
| `ProjectionDomain` | Adapter from the legacy delegate registration to `IProjectionDomain`. |
| `ProjectionHealthCoordinator` | Registry + health + dirty/degraded + main-thread rebuild pump. |
| `RemoteCharacterPresentation` | One concrete typed read model: remote player presentation. |

`ProjectionHealthCoordinator.Register(IProjectionDomain)` is the central entry.
Existing domain callers can migrate one line at a time through
`new ProjectionDomain(...)`.

## 4. Domain model

| Projection domain | Authoritative source | Typed read model / surface |
|---|---|---|
| Items | Kernel `ItemState` / committed batches | `WorldItemTable`, `RemoteInventorySnapshot`, clone inventory proxies |
| Fluids | Kernel `FluidRegionState` | `FluidKernelReadProjection.Regions`, RLE grid presentation |
| World entities | Kernel `WorldEntityState` | trap/opened/building health fact lists |
| Players | Kernel `PlayerState` + continuous snapshot | `RemoteCharacterPresentation`, `CharacterDataMsg` restore, render clones |
| Enemies | Kernel `EnemyState` | enemy presentation/health surfaces |
| Run/World | Kernel `RunState` | world run state mapping |

Every domain should eventually register through `IProjectionDomain` so the
health coordinator can rebuild *any* projection and operators can query the full
projection inventory in one place.

## 5. Migration status

Implemented now:

- Typed `IProjectionDomain` + `ProjectionDomain`.
- `ProjectionHealthCoordinator.Register(IProjectionDomain)`.
- Existing health-tracked domains (`items`, `fluids`, `world-entities`) now use
  the typed contract.
- A concrete typed read model for remote player presentation:
  `RemoteCharacterPresentation` (face, body pose, medical derived state,
  inventory view).
- Three additional runtime read-model domains are registered through the
  global contract:
  - `run` — `WorldService` maps the kernel `RunState` into the adapter-facing
    `WorldStartParams` projection and rebuilds it from `QueryRun()`.
  - `players-carry` — `PlayerKernelCarryProjection` rebuilds the
    `PlayerCarryService` carry mirror from the kernel player table.
  - `remote-character-presentation` — `RemoteCharacterPresentationStore` keeps
    deep copies of the latest character-data source snapshots and rebuilds the
    typed presentation model. It is the registry-backed domain read model;
    the existing `RemoteVitalsService` and `RemoteInventoryService` remain
    separate session caches for their UI-specific snapshot shapes.

Remaining migration:

- Player/enemy character-data classes that are currently write-side adapters,
  stateless restore appliers, or one-shot event fan-outs are not registered as
  `IProjectionDomain` because they do not have a rebuildable read model; this
  boundary is documented in the ticket.
- The remaining genuinely rebuildable read-model surfaces (mod-status local
  projection and any future enemy/runtime full-read projection) still need a
  separate design pass before they can join the inventory.
- `ProjectionHealthCoordinator.Snapshot()` is the observability surface for all
  currently registered projection domains; the existing domains and the three
  new domains above are all listed there.

## 6. Rules

1. A projection never writes authority.
2. A projection must be rebuildable from the current authoritative source.
3. A projection failure is contained by the registry, marked dirty, and retried
   on the main-thread pump.
4. A projection read model is explicit and Unity-free where possible.
5. Adding a new surface must not add a parallel per-field projection helper; it
   must either reuse an existing typed read model or create one that is
   registered under the global contract.
