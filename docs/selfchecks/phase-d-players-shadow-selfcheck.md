# Phase D Players shadow self-check (2026-08-29)

This fact sheet records the Phase D Players domain first cycle: a kernel
terminal-status table (alive/conscious), reset semantics, and
checkpoint/wire/save integration. The production player lifecycle and
cross-player interaction services still remain the live path; this model is the
next domain's shadow foundation.

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
| Runtime authority surface | `ItemKernelAuthority.TryUpdatePlayerStatus/TryResetPlayers/QueryPlayers` | Shadow entry points for the later production switch. |

## Evidence table

| Claim | Evidence |
|---|---|
| Kernel upserts and restores player status | `PlayerDomainKernelTests.UpdateStatus_UpsertsPlayerTableAndCheckpoint`. |
| Dead-but-conscious player is rejected | `PlayerDomainKernelTests.DeadConsciousPlayer_IsRejectedByInvariant`. |
| Reset clears the player table | `PlayerDomainKernelTests.ResetPlayers_ClearsTable`. |
| Wire batch preserves a player-status event | `PlayerDomainKernelTests.WireBatchRoundTrip_PreservesPlayerStatusEvent`. |
| Checkpoint chunks preserve players | `PlayerDomainKernelTests.CheckpointSplitAssemble_RoundTripsPlayers`. |
| Save/load preserves players | `PlayerDomainKernelTests.SaveLoad_RoundTripsPlayers`. |

## Verification

- `dotnet build CasualtiesUnknownOnline.slnx`: 0 warnings / 0 errors.
- `dotnet test CasualtiesUnknownOnline.slnx`: 1655 passed.
- `dotnet format`: applied.
- Architecture/event/entity/isolation gates passed.

## Structure review

- `GameState` remains dependency-free.
- One top-level type per new file.
- Player terminal facts are separated from high-frequency body stream fields in
  the domain model.

## Next sub-steps

1. Route player death/unconscious/limb terminal transitions and carry/release
   relations through kernel commands.
2. Project kernel player facts into `PlayerEntity`/character restore and
   snapshots.
3. Add cross-player interaction authority policies and tests.
