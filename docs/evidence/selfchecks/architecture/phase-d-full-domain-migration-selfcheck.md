# Phase D Full Domain Migration self-check (2026-08-30)

This fact sheet records Phase D completion. The authoritative fact base now
covers all six Phase D domains through typed kernel modules, checkpoint/save
round-trips, and network replay. Per-domain delivery details remain in the
domain fact sheets listed below; this file is the phase-level exit-criteria
summary plus the Phase E residue handoff.

## Mechanism inventory

| Domain | Kernel authority | Projection / wire path | Primary self-check |
|---|---|---|---|
| World / Run / Epoch | `RunState` + epoch-checked commands/batches | Host world params commit `StartRunCommand`/`AdvanceLayerCommand`; guest restore projects run facts; epoch isolation rejects old-epoch residue | `phase-d-world-run-epoch-shadow-selfcheck.md` |
| Traps / Building Entities | `WorldEntityState` (trap phase, one-shot consumption, building health) | `WorldEntityKernelProjection` + `TrapVisualReplay`; legacy world-entity snapshot wire removed | `phase-d-world-entities-shadow-selfcheck.md` |
| Players / cross-player | `PlayerStateTable` (status, limbs, body terminal latches, skills, carry) | `PlayerKernelStatusProjection`, `PlayerKernelCarryProjection`, `PlayerInteractionKernelProjection`, `PlayerKernelRestoreProjection`; legacy carry/result wires removed | `phase-d-players-shadow-selfcheck.md` |
| Enemy / Entity | `EnemyStateTable` (health/stun/prefab/runtime spawn + terminal `Removed`) | `EnemyCombatKernelProjection`, `EnemyKernelRestoreProjection`, `EnemyRemovedEvent`; legacy enemy result/removal wires removed | `phase-d-enemies-shadow-selfcheck.md` |
| Fluids | `FluidStateTable` region facts | Host grid commits coarse region checkpoints; guest RLE absolute-overwrite grid and `FluidKernelReadProjection` remain read-only seams | `phase-d-fluids-shadow-selfcheck.md` |
| High-frequency streams | Update-only `StateStreamEnvelope` over `KernelEnvelope` | Player/enemy 20 Hz streams; no aggregate creation/destruction/terminal rollback from streams | `phase-d-high-frequency-stream-unification-selfcheck.md` |

## Phase D exit criteria evidence

| Exit criterion | Evidence |
|---|---|
| All persistent gameplay facts have a single authoritative kernel write entry | Per-domain tables/commands above; old direct result/snapshot/carry wires are removed (greps find no production references to the removed `NetMsg` names). |
| Every domain has a typed module, checkpoint inclusion, and replay/reduce path | Domain self-checks document each table/command/event plus wire/checkpoint/save round-trip tests. |
| Cross-domain operations are atomic batches | `CompositeGameCommand`; trap trigger + building health + item drops commit as one atomic batch (tech-decisions.md #138/#144). |
| RunEpoch isolation enforced at every boundary | `EpochIsolationTests` cover all kernel domain tables; old-epoch commands/batches rejected. |
| Projections rebuildable from checkpoint + batches | Guest `CheckpointRestored`/`BatchApplied` projections exist per domain (world items, players, enemies, fluids, world entities, carry). |
| Old authoritative tables/caches are projections or removed | Per-domain self-checks and status notes identify the remaining Phase E residue (ad-hoc caches) separately from Phase D authority migration. |
| Domain isolation guards pass | Architecture/event/entity gates pass; `GameState` remains dependency-free. |
| Full suite + property/simulation tests pass | 1794 tests green before this documentation closure; the closure itself requires no code change. |
| User-observable replay semantics remain equivalent | Per-domain wire/checkpoint/replay tests and the full suite cover the migrated surfaces. |

## 4.3 prediction/rollback boundary

- **Closed by decision**, not by a new runtime. Cross-player take/heal/use/carry
  are `HostValidatedNoPrediction`; push is `PresentationOnly` (tech-decisions.md
  #154).
- The generic **Prediction Runtime** from current architecture design §7.2 is deferred to
  Phase E. It is not a Phase D exit requirement.
- Existing transient/rollback caches (`PickupOrigins`, pending-pickup queue,
  `DropPendingState`, `NativeOperationCoordinator`) remain in production as
  local/native correctness mechanisms and are Phase E residue candidates.

## Verification

- `dotnet build CasualtiesUnknownOnline.slnx`: 0 warnings / 0 errors (prior code state).
- `dotnet test CasualtiesUnknownOnline.slnx`: 1794 passed (prior code state).
- `dotnet format`, architecture, event-replay, entity-event-dispatch, and delivery
  gates passed in the accumulated Phase D cycles.
- This completion entry is a documentation-only change; no source/test/tool files were
  modified, so no new build/test run is required for the closure itself.

## Structure review

- One top-level type per new file; no new files in this closure.
- `GameState` remains dependency-free and is the only authoritative state layer.
- Runtime/GameAdapter surfaces are projections or narrow authority entry points.
- The remaining known Phase E residue is intentionally not labeled as Phase D
  authority state: ad-hoc prediction/rollback caches, the `ItemReject` frame
  survivor, and the `EnemyCombatOrderPolicy` kernel-process follow-up.
