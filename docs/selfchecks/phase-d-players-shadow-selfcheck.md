# Phase D Players shadow self-check (2026-08-29)

This fact sheet records the Phase D Players domain cycles: a kernel
terminal-status table (alive/conscious), discrete limb terminal facts, the
cross-player carry relation, reset semantics, checkpoint/wire/save
integration, and production wiring from the entity-sync surface, character
data and carry service into the kernel.

## Mechanism inventory

| Mechanism | Where | Notes |
|---|---|---|
| Player terminal state | `Domains/Players/PlayerState.cs` | SteamID-keyed alive/conscious facts plus discrete limb latches (`PlayerLimbState`) and body-level terminal latches (`PlayerBodyTerminalState`). |
| Player table | `Domains/Players/PlayerStateTable.cs` | Immutable snapshot with upsert. |
| Commands | `UpdatePlayerStatusCommand`, `ResetPlayersCommand`, `SetPlayerCarryCommand`, `ClearPlayerCarryCommand` | Host-only commands; reset clears for a new run; carry commands record/release one carrier/one carried relation. |
| Events | `PlayerStatusUpdatedEvent`, `PlayersResetEvent`, `PlayerCarrySetEvent`, `PlayerCarryClearedEvent` | Reduce into the player table. |
| Domain module | `PlayerDomainModule.cs` | Decide/reduce/invariant; dead players cannot be conscious, SteamIDs unique, carry relation reciprocal and conflict-free. |
| Kernel integration | `GameStateKernel`, `GameStateStore`, `KernelReadModel`, `MutableKernelState`, `GameCheckpoint` | `PlayerStateTable?` is now a kernel domain table and checkpoint field. |
| Wire DTOs | `WirePlayerState` | Protocol remains GameState-free. |
| Mapper/save | `KernelWireMapper`, `WireCheckpointAssembler`, `KernelSaveFileStore`, `KernelSaveFile` | Player facts round-trip through wire checkpoints and disk saves. |
| Runtime authority surface | `ItemKernelAuthority.TryUpdatePlayerStatus/TryResetPlayers/QueryPlayers/TrySetPlayerCarry/TryClearPlayerCarry` | Host commands and query entry points. |
| Entity-sync projection | `PlayerKernelStatusProjection` + `EntitySyncService` | Host `PublishLocalState`/`ApplyEntityState` project alive/conscious changes into kernel status; guests receive the kernel batch through the existing protocol path. |
| Limb projection | `PlayerKernelLimbProjection` + `CharacterDataStore` | Host character snapshots and limb-latch events project discrete limb latches into kernel `PlayerState`; event + 1 Hz snapshot fallback are both covered. |
| Body terminal projection | `PlayerKernelLimbProjection` + `CharacterDataStore` | The same character-data/limb-event projection also commits body-level terminal booleans (`Disfigured`, `EyeGone`, `BothEyesGone`, `HasPulmonaryEmbolism`, last-stand/neural booleans, `FibrillationForced`, `MindwipeScriptPresent/Active`) into `PlayerBodyTerminalState`. |
| Carry production projection | `PlayerKernelCarryProjection` + `PlayerCarryService` | Host carry mutations are kernel commands; `PlayerKernelCarryProjection` applies committed batches on the host (`BatchCommitted`) and guest (`BatchApplied`) and rebuilds from checkpoint restore. The carry mirror and `CarryStateChanged` now ride the same kernel batch; legacy `NetMsg.PlayerCarryState` and its handler are removed. |
| Cross-player item kernel sync | `PlayerInventoryTakeService` / `PlayerHealService` / `PlayerItemUseService` + `ItemKernelAuthority` | Host-recipient take, host-user heal/use, and wear-to-host now spawn/transfer/update/destroy the carried item in the item kernel, closing the host-side item-ownership gap; guest recipients continue through the transfer-table adopt path. |
| Push presentation policy | `PlayerPushService` + `PlayerPushResultMsg` | Push is transient presentation: no kernel command/event, no durable relation/health change; the host result stays a direct host→all presentation message and the resulting motion rides the 20 Hz player stream. |

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
| Guest carry mirror follows the committed kernel batch | `PlayerInteractionServiceTests.Guest_StartsCarryingUnconsciousHost_RecordsKernelCarryAndUpdatesMirrors` and `Host_StartsCarryingUnconsciousGuest_RecordsKernelCarryAndUpdatesGuestMirror` assert the guest mirror after the `KernelEnvelope` projection. |
| Carry stop projects a kernel clear and clears both mirrors | `PlayerInteractionServiceTests.Carry_Stop_ClearsRelationAndBroadcastsKernelClear`. |
| Host-recipient take commits the carried item to the kernel | `PlayerInteractionServiceTests.Host_TakesItemFromUnconsciousGuest_SendsTransferToGuest` asserts the kernel item is carried by the host. |
| Host-user consumable use commits the post-use item state to the kernel | `PlayerInteractionServiceTests.Host_UsesBreadOnGuest_AppliesFoodAndSendsResult` asserts the kernel item condition/owner. |
| Wear-to-host transfer commits the worn item to the kernel | `PlayerInteractionServiceTests.Guest_WearsHelmetOnHost_MovesItemAndSendsWornResult` asserts the kernel item is carried by the host after the transfer. |
| Guest replay kernel receives the same item facts through KernelEnvelope | The take, bread-use, and wear-to-host tests now also assert the guest `ItemKernelAuthority` sees the host-owned/post-use item state. |
| Destroyed guest-owned item leaves the kernel carried state | `PlayerInteractionServiceTests.Guest_UsesSplintOnHost_AppliesComponentAndDestroysItem` asserts the host and guest kernel entries are no longer `Carried`. |
| Kernel upserts limb terminal facts | `PlayerDomainKernelTests.UpdateStatus_UpsertsLimbFacts`. |
| Duplicate limb index is rejected | `PlayerDomainKernelTests.DuplicateLimbIndex_IsRejectedByInvariant`. |
| Wire/checkpoint/save preserve limb facts | `PlayerDomainKernelTests.WireBatchRoundTrip_PreservesPlayerLimbFacts` / `CheckpointSplitAssemble_RoundTripsPlayerLimbFacts` / `SaveLoad_RoundTripsPlayerLimbFacts`. |
| Host character data commits limb facts to kernel | `PlayerProjectionTests.HostSaveCharacterData_CommitsPlayerKernelLimbFacts`. |
| Host limb event commits limb facts to kernel | `PlayerProjectionTests.LimbStateEvent_CommitsPlayerKernelLimbFacts`. |
| Kernel upserts body terminal facts | `PlayerDomainKernelTests.UpdateStatus_UpsertsBodyTerminalFacts`. |
| Wire/checkpoint/save preserve body terminal facts | `PlayerDomainKernelTests.WireBatchRoundTrip_PreservesPlayerBodyTerminalFacts` / `CheckpointSplitAssemble_RoundTripsPlayerBodyTerminalFacts` / `SaveLoad_RoundTripsPlayerBodyTerminalFacts`. |
| Host character data commits body terminal facts | `PlayerProjectionTests.HostSaveCharacterData_CommitsPlayerKernelBodyTerminalFacts`. |
| Host limb event commits body terminal facts | `PlayerProjectionTests.LimbStateEvent_CommitsPlayerKernelBodyTerminalFacts`. |
| Cross-player use commits body terminal facts | `PlayerInteractionServiceTests.Guest_UsesMindwipeOnUnhappyHost_AppliesMindwipeScript` asserts kernel `PlayerBodyTerminalState`. |

## Verification

- `dotnet build CasualtiesUnknownOnline.slnx`: 0 warnings / 0 errors.
- `dotnet test CasualtiesUnknownOnline.slnx`: 1694 passed.
- `dotnet format`: applied.
- Architecture/event/entity/isolation gates passed.

## Structure review

- `GameState` remains dependency-free.
- One top-level type per new file.
- Player terminal facts are separated from high-frequency body stream fields in
  the domain model.

## Next sub-steps

1. [x] Add limb terminal facts to the player domain: `PlayerLimbState`,
   checkpoint/wire/save round-trip, and production projection from character
   data and limb-latch events.
2. [x] Add body-level terminal latches to the player domain:
   `PlayerBodyTerminalState`, checkpoint/wire/save round-trip, and production
   projection from character data, limb-latch events, and cross-player use.
3. [x] Remove the legacy carry-state wire: carry mirrors on host and guest now
   project from committed kernel batches; `NetMsg.PlayerCarryState`, its
   handler, and `FireCarryStateReceived` are removed.
4. [x] Confirm push is transient presentation: `PlayerPushService` creates no
   kernel command/event and `PlayerPushResultMsg` remains a direct host→all
   presentation message; the resulting motion falls back to the 20 Hz player
   stream. The remaining work in this item is the explicit command/event
   routing for take/heal/use result messages themselves.
5. Project kernel player facts into character restore/snapshots where the old
   snapshot stream is not sufficient.
