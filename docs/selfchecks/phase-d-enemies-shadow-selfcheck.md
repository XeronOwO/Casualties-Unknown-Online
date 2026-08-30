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
| Restore projection | `EnemyKernelRestoreProjection` + `EnemySyncService` | Host world-entry/reconnect snapshots and guest full-snapshot application overlay kernel `EnemyStateTable` terminal facts (health, stunned, prefab, runtime-spawn) onto `EnemyEntity`; continuous presentation fields (position/velocity/rotation/legs/telegraph) remain from the snapshot/stream. |

## Evidence table

| Claim | Evidence |
|---|---|
| Kernel upserts/removes/resets enemies | `EnemyDomainKernelTests.UpsertRemoveAndReset_DriveEnemyTable`. |
| Invalid health rejected | `EnemyDomainKernelTests.NegativeHealth_IsRejectedByInvariant`. |
| Wire batch preserves an enemy event | `EnemyDomainKernelTests.WireBatchRoundTrip_PreservesEnemyUpsertedEvent`. |
| Checkpoint chunks preserve enemies | `EnemyDomainKernelTests.CheckpointSplitAssemble_RoundTripsEnemies`. |
| Save/load preserves enemies | `EnemyDomainKernelTests.SaveLoad_RoundTripsEnemies`. |
| Host enemy publish commits kernel facts | `EnemyProjectionTests.HostPublishEnemyStates_CommitsKernelEnemyTable`. |
| Host world-entry snapshot projects kernel terminal facts | `EnemySyncServiceTests.HostSendEnemySnapshot_ProjectsKernelTerminalFacts`. |
| Guest full-snapshot apply projects kernel terminal facts | `EnemySyncServiceTests.GuestApplyEnemySnapshot_ProjectsKernelTerminalFacts` (health, stunned, prefab, runtime-spawn from kernel; snapshot position remains). |

## Verification

- `dotnet build CasualtiesUnknownOnline.slnx`: 0 warnings / 0 errors.
- `dotnet test CasualtiesUnknownOnline.slnx`: 1703 passed.
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
2. [x] Project kernel enemy facts into `EnemyEntity` restore/snapshot paths:
   `EnemyKernelRestoreProjection` overlays kernel health/stunned/prefab/runtime-spawn
   onto host world-entry snapshots and guest full-snapshot application;
   continuous presentation fields remain snapshot/stream-owned.
3. Continue high-frequency stream unification: the 20 Hz enemy batch is now
   update-only with explicit `EnemyRemovedMsg` lifecycle handling; see
   `docs/selfchecks/phase-d-high-frequency-stream-unification-selfcheck.md`.
