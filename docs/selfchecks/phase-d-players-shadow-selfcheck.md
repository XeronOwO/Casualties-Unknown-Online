# Phase D Players shadow self-check (2026-08-29)

This fact sheet records the Phase D Players domain first cycles: a kernel
terminal-status table (alive/conscious), reset semantics,
checkpoint/wire/save integration, and production wiring from the entity-sync
surface into the kernel for host-side player status changes. Cross-player
carry/release and richer terminal facts remain for subsequent cycles.

## Mechanism inventory

| Mechanism | Where | Notes |
|---|---|---|
| Player terminal state | `Domains/Players/PlayerState.cs` | SteamID-keyed alive/conscious facts. |
| Player table | `Domains/Players/PlayerStateTable.cs` | Immutable snapshot with upsert. |
| Commands | `UpdatePlayerStatusCommand`, `ResetPlayersCommand` | Host-only shadow commands; reset clears for a new run. |
| Events | `PlayerStatusUpdatedEvent`, `PlayersResetEvent` | Reduce into the table. |
| Domain module | `PlayerDomainModule.cs` | Decide/reduce/invariant; dead players cannot be conscious, SteamIDs unique. |
| Kernel integration | `GameStateKernel`, `GameStateStore`, `KernelReadModel`, `MutableKernelState`, `GameCheckpoint` | `PlayerStateTable?` is now a kernel domain table and checkpoint field. |
| Wire DTOs | `WirePlayerState` | Protocol remains GameState-free. |
| Mapper/save | `KernelWireMapper`, `WireCheckpointAssembler`, `KernelSaveFileStore`, `KernelSaveFile` | Player facts round-trip through wire checkpoints and disk saves. |
| Runtime authority surface | `ItemKernelAuthority.TryUpdatePlayerStatus/TryResetPlayers/QueryPlayers` | Host commands and query entry points. |
| Entity-sync projection | `PlayerKernelStatusProjection` + `EntitySyncService` | Host `PublishLocalState`/`ApplyEntityState` project alive/conscious changes into kernel status; guests receive the kernel batch through the existing protocol path. |

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

## Verification

- `dotnet build CasualtiesUnknownOnline.slnx`: 0 warnings / 0 errors.
- `dotnet test CasualtiesUnknownOnline.slnx`: 1656 passed.
- `dotnet format`: applied.
- Architecture/event/entity/isolation gates passed.

## Structure review

- `GameState` remains dependency-free.
- One top-level type per new file.
- Player terminal facts are separated from high-frequency body stream fields in
  the domain model.

## Next sub-steps

1. Add limb terminal facts and carry/release relations to the player domain.
2. Route cross-player interaction result messages through kernel commands.
3. Project kernel player facts into character restore/snapshots where the old
   ict stream is not sufficient.
