# Projection failure auto-recovery — self-check

Backlog item `projection-failure-auto-recovery.md`: kernel commits must not roll
back because a downstream Unity projection failed, but the current runtime had
no generic dirty marking/rebuild loop. This cycle adds a lightweight per-domain
projection health coordinator and wires the first rebuildable domains into it.

## 1. Mechanism inventory

| Mechanism | Prior state |
|---|---|
| Kernel commit | `GameStateKernel` commits atomically and raises `BatchCommitted` / `BatchApplied`; projection code runs outside the transaction. |
| Projection failure | A throwing projection subscriber propagates out of the authority event and can leave the Unity/remote view stale; no dirty marker or automatic rebuild existed. |
| Rebuild paths | Item world table/checkpoint rebuild, fluid read projection rebuild, and world-entity checkpoint projection already existed as per-domain rebuild paths, but no generic health wrapper called them after a failure. |

## 2. Change

- Added `ProjectionHealthCoordinator` (Runtime) as an `ICuoService`:
  - registers a projection domain with a kernel read-model rebuild callback;
  - wraps `Run(domain, revision, projection)` so a thrown projection is captured,
    the domain is marked dirty, and the last failed revision is recorded;
  - pumps dirty domains on the Unity main thread through `Pump()` / `Update()`;
  - tracks last successful revision, consecutive/total failures, last error, and
    escalates to `Degraded` after three consecutive failures.
- Wired the first production domains:
  - `ItemService` / `KernelBatchItemProjection`: `items` domain; batch apply,
    checkpoint restore, and `RebuildFromKernel()` (world table + carried sync).
  - `FluidKernelReadProjection`: `fluids` domain; batch/checkpoint projection and
    `RebuildFromKernel()` from `QueryFluids()`.
  - `WorldEntityKernelProjection`: `world-entities` domain; checkpoint projection
    and `RebuildFromKernel()` from `QueryWorldEntities()`.
- The coordinator is intentionally per-domain: recovery calls the domain's own
  kernel read-model rebuild instead of a full checkpoint replay.

## 3. Verification

| Evidence | Result |
|---|---|
| `ProjectionHealthCoordinatorTests` (success, failure, pump rebuild, repeated-failure escalation, rebuild-failure retry, duplicate registration) | 6/6 passed |
| `ProjectionFailure_MarksDirtyAndRebuildsWithoutRevertingKernel` (real guest protocol path: ItemSpawned handler throws, kernel stays correct, dirty marked, main-thread pump rebuilds) | Passed |
| `dotnet test CasualtiesUnknownOnline.slnx` | 2177 passed |
| `dotnet build CasualtiesUnknownOnline.slnx` | 0 warnings / 0 errors |

## 4. Remaining scope

The generic coordinator is available to every projection domain. Current
production adoption covers the item world projection, fluid read projection, and
world-entity checkpoint projection. Domains whose "rebuild from kernel read
model" path is not yet defined (for example event-result presentation projections
that only replay from batches) remain on the same registration contract but are
not wired yet; this ticket intentionally does not invent a full checkpoint-replay
recovery mechanism.
