# Phase D WorldEntities self-check (2026-08-29)

This fact sheet records the Phase D Traps/Building Entities domain cycle: the
WorldEntities kernel domain (one-shot trap consumptions, building-entity
health, opened lockable entities), its checkpoint/wire/save round-trip, the
production authority switch, and the conversion of the three runtime registries
into kernel-backed projection/snapshot adapters.

## Mechanism inventory

| Mechanism | Where | Notes |
|---|---|---|
| Entity position | `Domains/WorldEntities/EntityPosition.cs` | Integer world-cell identity; matches the runtime position-keyed registries. |
| Facts | `TrapConsumptionFact`, `BuildingEntityHealthFact`, `OpenedEntityFact` | One authoritative fact for one-shot traps, building health, and opened entities. |
| Aggregate | `WorldEntityState.cs` | Immutable snapshot with idempotent/upsert semantics. |
| Commands | `RecordTrapConsumedCommand`, `RecordBuildingEntityHealthCommand`, `RecordOpenedEntityCommand`, `ResetWorldEntitiesCommand` | Host-only commands for the authority model; reset clears all world-entity facts for a new layer. |
| Events | `TrapConsumedEvent`, `BuildingEntityHealthUpdatedEvent`, `OpenedEntityEvent`, `WorldEntitiesResetEvent` | Reduce into the aggregate. |
| Domain module | `WorldEntityDomainModule.cs` | Decide/reduce/invariant; rejects invalid trap kind/health and preserves the runtime table caps. |
| Kernel integration | `GameStateKernel`, `GameStateStore`, `KernelReadModel`, `MutableKernelState`, `GameCheckpoint` | `WorldEntityState?` is now a kernel domain table and checkpoint field. |
| Wire DTOs | `WireWorldEntityState`, `WireTrapConsumption`, `WireBuildingEntityHealth`, `WireOpenedEntity`, `WireEntityPosition` | Protocol remains GameState-free. |
| Mapper/save | `KernelWireMapper`, `WireCheckpointAssembler`, `KernelSaveFileStore`, `KernelSaveFile` | World-entity facts round-trip through wire checkpoints and disk saves. |
| Runtime authority surface | `ItemKernelAuthority.TryRecordTrapConsumed/TryRecordBuildingEntityHealth/TryRecordOpenedEntity/TryResetWorldEntities/QueryWorldEntities` | Host commands and query entry points. |
| Registry projections | `TrapConsumptionRegistry`, `OpenedEntityRegistry`, `BuildingEntityHealthRegistry` | The three old registries now commit through the kernel and build their snapshot payloads from `QueryWorldEntities`; they are no longer independent fact stores. |

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
| Reset command clears all world-entity facts | `WorldEntityDomainKernelTests.ResetWorldEntities_ClearsAllFacts`. |
| Host world control reports commit kernel facts | `WorldEntityProjectionTests.HostReports_CommitKernelWorldEntities`. |

## Verification

- `dotnet build CasualtiesUnknownOnline.slnx`: 0 warnings / 0 errors.
- `dotnet test CasualtiesUnknownOnline.slnx`: 1649 passed.
- `dotnet format`: applied.
- Architecture/event/entity/isolation gates passed.

## Structure review

- `GameState` remains dependency-free: WorldEntities uses only System collections
  and kernel primitives.
- One top-level type per new file.
- The domain sits behind the same `IDomainModule` seam as Items and World/Run.

## Next sub-steps

1. Optionally project kernel `WorldEntityState` directly into guest adapter
   events on checkpoint/batch restore and remove the legacy snapshot frames
   (`TrapStateSnapshot`, `OpenedEntitiesSnapshot`,
   `BuildingEntityHealthSnapshot`) as a finishing cleanup.
2. Move on to the next Phase D domain (Players terminal state/cross-player
   interaction).
