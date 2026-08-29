# Phase D Players shadow self-check (2026-08-29)

This fact sheet records the Phase D Players domain cycles: a kernel
terminal-status table (alive/conscious), the cross-player carry relation,
reset semantics, checkpoint/wire/save integration, and production wiring from
the entity-sync surface and carry service into the kernel. Richer terminal
facts (limbs etc.) remain for subsequent cycles.

## Mechanism inventory

| Mechanism | Where | Notes |
|---|---|---|
| Player terminal state | `Domains/Players/PlayerState.cs` | SteamID-keyed alive/conscious facts. |
| Player table | `Domains/Players/PlayerStateTable.cs` | Immutable snapshot with upsert. |
| Commands | `UpdatePlayerStatusCommand`, `ResetPlayersCommand`, `SetPlayerCarryCommand`, `ClearPlayerCarryCommand` | Host-only commands; reset clears for a new run; carry commands record/release one carrier/one carried relation. |
| Events | `PlayerStatusUpdatedEvent`, `PlayersResetEvent`, `PlayerCarrySetEvent`, `PlayerCarryClearedEvent` | Reduce into the player table. |
| Domain module | `PlayerDomainModule.cs` | Decide/reduce/invariant; dead players cannot be conscious, SteamIDs unique, carry relation reciprocal and conflict-free. |
| Kernel integration | `GameStateKernel`, `GameStateStore`, `KernelReadModel`, `MutableKernelState`, `GameCheckpoint` | `PlayerStateTable?` is now a kernel domain table and checkpoint field. |
| Wire DTOs | `WirePlayerState` | Protocol remains GameState-free. |
| Mapper/save | `KernelWireMapper`, `WireCheckpointAssembler`, `KernelSaveFileStore`, `KernelSaveFile` | Player facts round-trip through wire checkpoints and disk saves. |
| Runtime authority surface | `ItemKernelAuthority.TryUpdatePlayerStatus/TryResetPlayers/QueryPlayers/TrySetPlayerCarry/TryClearPlayerCarry` | Host commands and query entry points. |
| Entity-sync projection | `PlayerKernelStatusProjection` + `EntitySyncService` | Host `PublishLocalState`/`ApplyEntityState` project alive/conscious changes into kernel status; guests receive the kernel batch through the existing protocol path. |
| Carry production projection | `PlayerKernelCarryProjection` + `PlayerCarryService` | Host `PublishCarryState` also commits the relation into the kernel; guests keep the legacy wire mirror while the kernel owns the durable fact. |

## Evidence table

| Claim | Evidence |
|---|---|
| Kernel upserts and restores player status | `PlayerDomainKernelTests.UpdateStatus_UpsertsPlayerTableAndCheckpoint`. |
| Dead-but-conscious player is rejected | `PlayerDomainKernelTests.DeadConsciousPlayer_IsRejectedByInvariant`. |
| Reset clears the player table | `PlayerDomainKernelTests.ResetPlayers_ClearsTable`. |
| Wire batch preserves a player-status event | `PlayerDomainKernelTests.WireBatchRoundTrip_PreservesPlayerStatusEvent`. |
| Checkpoint chunks preserve players | `PlayerDomainKernelTests.CheckpointSplitAssemble_RoundTripsPlayers`. |
| Save/load preserves players | `PlayerDomainKernelTests.SaveLoad_RoundTripsPlayers`. |
| Host entity-sync publish commits player status | `PlayerProjectionTests.HostPublishLocalState_CommitsPlayerKernelStatus`. |
| Carry set/clear drives reciprocal player relation | `PlayerDomainKernelTests.SetAndClearCarry_DrivePlayerRelation`. |
| Self carry and carry conflicts are rejected | `PlayerDomainKernelTests.SelfCarry_IsRejected` / `CarryConflict_IsRejected`. |
| Wire batch preserves a carry event | `PlayerDomainKernelTests.WireBatchRoundTrip_PreservesPlayerCarryEvent`. |
| Checkpoint/save preserve carry fields | `PlayerDomainKernelTests.CheckpointSplitAssemble_RoundTripsPlayerCarryFields` / `SaveLoad_RoundTripsPlayerCarryFields`. |
| Host carry service commits kernel carry | `PlayerInteractionServiceTests.Guest_StartsCarryingHost_CommitsToKernelAndClearsOnStop`. |

## Verification

- `dotnet build CasualtiesUnknownOnline.slnx`: 0 warnings / 0 errors.
- `dotnet test CasualtiesUnknownOnline.slnx`: 1683 passed.
- `dotnet format`: applied.
- Architecture/event/entity/isolation gates passed.

## Structure review

- `GameState` remains dependency-free.
- One top-level type per new file.
- Player terminal facts are separated from high-frequency body stream fields in
  the domain model.

## Next sub-steps

1. Add limb terminal facts to the player domain.
2. Route other cross-player interaction results (take/heal/use/push) through
   kernel commands where they carry durable facts.
3. Project kernel player facts into character restore/snapshots where the old
   snapshot stream is not sufficient.
