# Phase D Enemies/Entities shadow self-check (2026-08-29)

This fact sheet records the Phase D Enemy/Entity domain cycles: a kernel
enemy/entity lifecycle-health table, checkpoint/wire/save integration, and
production wiring from the host enemy-sync publish path into the kernel.
High-frequency enemy position/velocity remains a stream; the kernel owns the
durable entity identity/health/runtime-spawn facts.

## Mechanism inventory

| Mechanism | Where | Notes |
|---|---|---|
| Entity identity | `Domains/Entities/EntityId.cs` | Epoch/counter/generation matching the Runtime NetworkEntityId shape. |
| Enemy fact | `Domains/Entities/EnemyState.cs` | Prefab, health, runtime-spawn, stunned. |
| Enemy table | `Domains/Entities/EnemyStateTable.cs` | Immutable snapshot with upsert/remove. |
| Commands | `UpsertEnemyCommand`, `RemoveEnemyCommand`, `ResetEnemiesCommand` | Host-only lifecycle/health commands. |
| Events | `EnemyUpsertedEvent`, `EnemyRemovedEvent`, `EnemiesResetEvent` | Reduce into the table. |
| Domain module | `EnemyDomainModule.cs` | Decide/reduce/invariant; invalid health rejected, unique entity ids enforced. |
| Kernel integration | `GameStateKernel`, `GameStateStore`, `KernelReadModel`, `MutableKernelState`, `GameCheckpoint` | `EnemyStateTable?` is a kernel domain table and checkpoint field. |
| Wire DTOs | `WireEnemyState`, `WireEntityId` | Protocol remains GameState-free. |
| Mapper/save | `KernelDomainWireMapper`, `KernelWireMapper`, `WireCheckpointAssembler`, `KernelSaveFileStore`, `KernelSaveFile` | Enemy facts round-trip through wire checkpoints and disk saves. |
| Entity-sync projection | `EnemyKernelProjection` + `EnemySyncService` | Host `PublishEnemyStates` change-gated projects the enemy set into kernel and removes stale entries. |

## Evidence table

| Claim | Evidence |
|---|---|
| Kernel upserts/removes/resets enemies | `EnemyDomainKernelTests.UpsertRemoveAndReset_DriveEnemyTable`. |
| Invalid health rejected | `EnemyDomainKernelTests.NegativeHealth_IsRejectedByInvariant`. |
| Wire batch preserves an enemy event | `EnemyDomainKernelTests.WireBatchRoundTrip_PreservesEnemyUpsertedEvent`. |
| Checkpoint chunks preserve enemies | `EnemyDomainKernelTests.CheckpointSplitAssemble_RoundTripsEnemies`. |
| Save/load preserves enemies | `EnemyDomainKernelTests.SaveLoad_RoundTripsEnemies`. |
| Host enemy publish commits kernel facts | `EnemyProjectionTests.HostPublishEnemyStates_CommitsKernelEnemyTable`. |

## Verification

- `dotnet build CasualtiesUnknownOnline.slnx`: 0 warnings / 0 errors.
- `dotnet test CasualtiesUnknownOnline.slnx`: 1662 passed.
- `dotnet format`: applied.
- Architecture/event/entity/isolation gates passed; architecture split added
  `ItemKernelCodec` and `KernelDomainWireMapper` to keep large types under 600.

## Structure review

- `GameState` remains dependency-free.
- One top-level type per new file.
- The Enemy domain is isolated behind the same `IDomainModule` seam.

## Next sub-steps

1. Add enemy combat/terminal-state events (bite/lunge/effect/targeting) as
   cross-domain batches.
2. Project kernel enemy facts into `EnemyEntity` restore/snapshot paths.
3. Move to Fluids persistent region domain.
