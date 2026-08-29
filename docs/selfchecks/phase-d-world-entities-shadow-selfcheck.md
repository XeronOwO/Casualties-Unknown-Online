# Phase D WorldEntities shadow self-check (2026-08-29)

This fact sheet records the second Phase D domain cycle: the WorldEntities
kernel domain (one-shot trap consumptions, building-entity health, opened
lockable entities), its checkpoint/wire/save round-trip, and runtime authority
convenience surfaces. The production registries still remain the live
authoritative path; the kernel shadow model is now present for the next
authority-switch cycle.

## Mechanism inventory

| Mechanism | Where | Notes |
|---|---|---|
| Entity position | `Domains/WorldEntities/EntityPosition.cs` | Integer world-cell identity; matches the runtime position-keyed registries. |
| Facts | `TrapConsumptionFact`, `BuildingEntityHealthFact`, `OpenedEntityFact` | One authoritative fact for one-shot traps, building health, and opened entities. |
| Aggregate | `WorldEntityState.cs` | Immutable snapshot with idempotent/upsert semantics. |
| Commands | `RecordTrapConsumedCommand`, `RecordBuildingEntityHealthCommand`, `RecordOpenedEntityCommand` | Host-only commands for the shadow model. |
| Events | `TrapConsumedEvent`, `BuildingEntityHealthUpdatedEvent`, `OpenedEntityEvent` | Reduce into the aggregate. |
| Domain module | `WorldEntityDomainModule.cs` | Decide/reduce/invariant; rejects invalid trap kind and invalid health. |
| Kernel integration | `GameStateKernel`, `GameStateStore`, `KernelReadModel`, `MutableKernelState`, `GameCheckpoint` | `WorldEntityState?` is now a kernel domain table and checkpoint field. |
| Wire DTOs | `WireWorldEntityState`, `WireTrapConsumption`, `WireBuildingEntityHealth`, `WireOpenedEntity`, `WireEntityPosition` | Protocol remains GameState-free. |
| Mapper/save | `KernelWireMapper`, `WireCheckpointAssembler`, `KernelSaveFileStore`, `KernelSaveFile` | World-entity facts round-trip through wire checkpoints and disk saves. |
| Runtime authority surface | `ItemKernelAuthority.TryRecordTrapConsumed/TryRecordBuildingEntityHealth/TryRecordOpenedEntity/QueryWorldEntities` | Shadow entry points for the later production switch. |

## Evidence table

| Claim | Evidence |
|---|---|
| Kernel records and queries all three fact kinds | `WorldEntityDomainKernelTests.RecordFacts_UpdatesKernelWorldEntityState`. |
| Same-position trap consumption upserts | `WorldEntityDomainKernelTests.SamePositionTrap_UpsertsInsteadOfDuplicating`. |
| Same-position opened entity is idempotent | `WorldEntityDomainKernelTests.SamePositionOpened_IsIdempotent`. |
| A committed batch replays on a guest kernel | `WorldEntityDomainKernelTests.Apply_RunEntityBatch_ReplaysFactsOnGuestKernel`. |
| Wire batch preserves a trap event | `WorldEntityDomainKernelTests.WireBatchRoundTrip_PreservesTrapConsumedEvent`. |
| Checkpoint chunks preserve the aggregate | `WorldEntityDomainKernelTests.CheckpointSplitAssemble_RoundTripsWorldEntityState`. |
| Save/load preserves the aggregate | `WorldEntityDomainKernelTests.SaveLoad_RoundTripsWorldEntityState`. |

## Verification

- `dotnet build CasualtiesUnknownOnline.slnx`: 0 warnings / 0 errors.
- `dotnet test CasualtiesUnknownOnline.slnx`: 1647 passed.
- `dotnet format`: applied.
- Architecture/event/entity/isolation gates passed.

## Structure review

- `GameState` remains dependency-free: WorldEntities uses only System collections
  and kernel primitives.
- One top-level type per new file.
- The domain sits behind the same `IDomainModule` seam as Items and World/Run.

## Next sub-steps

1. Route `TrapConsumptionRegistry`, `OpenedEntityRegistry`, and
   `BuildingEntityHealthRegistry` writes through the kernel commands.
2. Project kernel `WorldEntityState` into the legacy snapshot messages or replace
   them with checkpoint/checkpoint-stream payloads.
3. Delete the old registries as authoritative stores after the projection path is
   covered by session/replay tests.
