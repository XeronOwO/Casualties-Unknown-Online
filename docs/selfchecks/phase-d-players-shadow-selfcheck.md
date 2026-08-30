# Phase D Players shadow self-check (2026-08-29)

This fact sheet records the Phase D Players domain cycles: a kernel
terminal-status table (alive/conscious), discrete limb terminal facts, the
cross-player carry relation, reset semantics, checkpoint/wire/save
integration, and production wiring from the entity-sync surface, character
data and carry service into the kernel.

## Mechanism inventory

| Mechanism | Where | Notes |
|---|---|---|
| Player terminal state | `Domains/Players/PlayerState.cs` | SteamID-keyed alive/conscious facts plus discrete limb latches (`PlayerLimbState`), body-level terminal latches (`PlayerBodyTerminalState`), and durable skill facts (`PlayerSkillsState`). |
| Player table | `Domains/Players/PlayerStateTable.cs` | Immutable snapshot with upsert. |
| Commands | `UpdatePlayerStatusCommand`, `ResetPlayersCommand`, `SetPlayerCarryCommand`, `ClearPlayerCarryCommand` | Host-only commands; reset clears for a new run; carry commands record/release one carrier/one carried relation. |
| Events | `PlayerStatusUpdatedEvent`, `PlayersResetEvent`, `PlayerCarrySetEvent`, `PlayerCarryClearedEvent` | Reduce into the player table. |
| Domain module | `PlayerDomainModule.cs` | Decide/reduce/invariant; dead players cannot be conscious, SteamIDs unique, carry relation reciprocal and conflict-free, and a carrier must be alive/conscious. |
| Kernel integration | `GameStateKernel`, `GameStateStore`, `KernelReadModel`, `MutableKernelState`, `GameCheckpoint` | `PlayerStateTable?` is now a kernel domain table and checkpoint field. |
| Wire DTOs | `WirePlayerState` / `WirePlayerSkills` | Protocol remains GameState-free. |
| Mapper/save | `KernelWireMapper`, `WireCheckpointAssembler`, `KernelSaveFileStore`, `KernelSaveFile` | Player facts round-trip through wire checkpoints and disk saves. |
| Runtime authority surface | `ItemKernelAuthority.TryUpdatePlayerStatus/TryResetPlayers/QueryPlayers/TrySetPlayerCarry/TryClearPlayerCarry` | Host commands and query entry points. |
| Entity-sync projection | `PlayerKernelStatusProjection` + `EntitySyncService` | Host `PublishLocalState`/`ApplyEntityState` project alive/conscious changes into kernel status; guests receive the kernel batch through the existing protocol path. |
| Player roster ensure | `PlayerKernelStatusProjection.Ensure` + `EntitySyncService.StartMemberSync` | The host creates a default kernel player row the moment a member's entity sync starts, so the player-domain identity floor does not depend on the first 20 Hz report or 1 Hz character snapshot. |
| Authority policy | `PlayerInteractionAuthority` + `PlayerInteractionAuthorityPolicy` | Explicitly labels cross-player take/heal/use/carry as `HostValidatedNoPrediction` and push as `PresentationOnly`; `PlayerInteractionResultAuthority` resolves those policies to kernel `AuthorityKind` when journaling results. |
| Limb projection | `PlayerKernelLimbProjection` + `CharacterDataStore` | Host character snapshots and limb-latch events project discrete limb latches into kernel `PlayerState`; event + 1 Hz snapshot fallback are both covered. |
| Body terminal projection | `PlayerKernelLimbProjection` + `CharacterDataStore` | The same character-data/limb-event projection also commits body-level terminal booleans (`Disfigured`, `EyeGone`, `BothEyesGone`, `HasPulmonaryEmbolism`, last-stand/neural booleans, `FibrillationForced`, `MindwipeScriptPresent/Active`) into `PlayerBodyTerminalState`. |
| Skills projection | `PlayerKernelLimbProjection` + `CharacterDataStore` | Host character snapshots now commit durable `PlayerSkillsState` (strength/resistance/intelligence plus exp values) into the kernel player row; `PlayerKernelRestoreProjection` overlays those skills onto reconnect/re-entry snapshots. The character snapshot remains the continuous/fallback projection surface. |
| Carry production projection | `PlayerKernelCarryProjection` + `PlayerCarryService` | Host carry mutations are kernel commands; `PlayerKernelCarryProjection` applies committed batches on the host (`BatchCommitted`) and guest (`BatchApplied`) and rebuilds from checkpoint restore. The carry mirror and `CarryStateChanged` now ride the same kernel batch; legacy `NetMsg.PlayerCarryState` and its handler are removed. |
| Cross-player item kernel sync | `PlayerInventoryTakeService` / `PlayerHealService` / `PlayerItemUseService` + `ItemKernelAuthority` | Host-recipient take, host-user heal/use, and wear-to-host now spawn/transfer/update/destroy the carried item in the item kernel, closing the host-side item-ownership gap; guest recipients continue through the transfer-table adopt path. |
| Player interaction result projection | `PlayerInteractionKernelProjection` + `PlayerInteractionResultAuthority` + `PlayerInteractionKernelCodec` | Take/heal/use results are recorded as journal-only Players domain events; the projection restores `TransferReceived` / `HealReceived` / `UseReceived` on the host (`BatchCommitted`) and guests (`BatchApplied`). Legacy `NetMsg.PlayerInventoryTransfer` / `PlayerHealResult` / `PlayerItemUseResult` IDs and handlers are removed. |
| Push presentation policy | `PlayerPushService` + `PlayerPushResultMsg` | Push is transient presentation: no kernel command/event, no durable relation/health change; the host result stays a direct host→all presentation message and the resulting motion rides the 20 Hz player stream. |
| Restore projection | `PlayerKernelRestoreProjection` + `CharacterDataStore.SendSavedCharacter` | Reconnect/re-entry restores are projected from the kernel players table over the saved character snapshot: alive/conscious, limb latches, body-terminal latches, limb identity, and durable skill facts are authoritative; continuous physiological values, items, and position remain owned by the snapshot. Carry is not a character-snapshot field and is restored separately by `PlayerKernelCarryProjection` from checkpoints/committed batches. |

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
| Host start-member-sync ensures a kernel roster row | `PlayerProjectionTests.HostStartMemberSync_EnsuresKernelPlayerRosterRow`. |
| Cross-player authority policies are explicit and locked | `PlayerInteractionAuthorityPolicyTests` locks take/heal/use/carry as `HostValidatedNoPrediction`, push as `PresentationOnly`, and the kernel `AuthorityKind` mapping. |
| Carry set/clear drives reciprocal player relation | `PlayerDomainKernelTests.SetAndClearCarry_DrivePlayerRelation`. |
| Self carry and carry conflicts are rejected | `PlayerDomainKernelTests.SelfCarry_IsRejected` / `CarryConflict_IsRejected`. |
| Dead or unconscious carrier is rejected by invariant | `PlayerDomainKernelTests.DeadCarrier_IsRejectedByInvariant` / `UnconsciousButAliveCarrier_IsRejectedByInvariant`. |
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
| Result events commit and wire-round-trip | `PlayerDomainKernelTests.RecordPlayerInventoryTransfer_CommitsJournalEvent`, `WireBatchRoundTrip_PreservesPlayerInventoryTransferEvent`, `WireBatchRoundTrip_PreservesPlayerHealResultEvent`, and `WireBatchRoundTrip_PreservesPlayerItemUseResultEvent`. |
| Host and guest projection restore result events | `PlayerInteractionServiceTests.Guest_TakeResult_ProjectsTransferEventOnBothParticipants`, `Guest_HealResult_ProjectsHealEventOnBothParticipants`, and `Guest_UseResult_ProjectsUseEventOnBothParticipants`. |
| Legacy result wire removed | `NetMsg.PlayerInventoryTransfer`, `NetMsg.PlayerHealResult`, and `NetMsg.PlayerItemUseResult` have no production/test references; their old handlers are deleted. |
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
| Kernel upserts skill facts | `PlayerDomainKernelTests.UpdateStatus_UpsertsSkills`. |
| Wire/checkpoint/save preserve skill facts | `PlayerDomainKernelTests.WireBatchRoundTrip_PreservesPlayerSkills` / `CheckpointSplitAssemble_RoundTripsPlayerSkills` / `SaveLoad_RoundTripsPlayerSkills`. |
| Host character data commits skill facts to kernel | `PlayerProjectionTests.HostSaveCharacterData_CommitsPlayerKernelSkills`. |
| Reconnect restore projects kernel skills over the saved snapshot | `CharacterDataStoreTests.SendSavedCharacter_ProjectsKernelSkillsOverSnapshot`. |
| Reconnect restore projects kernel terminal facts over the saved snapshot | `CharacterDataStoreTests.SendSavedCharacter_ProjectsKernelTerminalFactsOverSnapshot` (alive/conscious, all body-terminal latches, limb latches) and `SendSavedCharacter_AddsKernelLimbFactMissingFromSnapshot`. |
| Restore projection preserves continuous limb/physiological snapshot fields | `CharacterDataStoreTests.SendSavedCharacter_ProjectsKernelTerminalFactsOverSnapshot` asserts skin/muscle health remain from the snapshot while kernel latches override. |

## Verification

- `dotnet build CasualtiesUnknownOnline.slnx`: 0 warnings / 0 errors.
- `dotnet test CasualtiesUnknownOnline.slnx`: 1792 passed.
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
   stream.
5. [x] Route take/heal/use result messages through kernel commands/events:
   `RecordPlayerInventoryTransferCommand` / `RecordPlayerHealResultCommand` /
   `RecordPlayerItemUseResultCommand` journal the result facts;
   `PlayerInteractionKernelProjection` restores the Game Adapter event surface
   on both host and guest from committed/replayed kernel batches; legacy
   `NetMsg.PlayerInventoryTransfer` / `PlayerHealResult` / `PlayerItemUseResult`
   and their handlers are removed.
5. [x] Project kernel player terminal facts into character restore/reconnect
   snapshots: `PlayerKernelRestoreProjection` overlays alive/conscious, limb
   latches, body-terminal latches, and limb identity from `PlayerStateTable`
   onto the saved `CharacterDataMsg` before `SendSavedCharacter` hands it back;
   continuous physiological values/items/position remain snapshot-owned and
   carry continues through the checkpoint/committed-batch projection.
6. [x] Add durable skill facts to the player domain: `PlayerSkillsState`,
   checkpoint/wire/save round-trip, host character-data projection, and
   reconnect restore overlay through `PlayerKernelRestoreProjection`.
7. [x] Ensure the player identity floor in the kernel: host entity-sync start
   creates a default `PlayerState` row via `PlayerKernelStatusProjection.Ensure`,
   so carry/relation validation no longer waits for the first high-frequency
   player report.
8. [x] Define explicit cross-player authority policies:
   `PlayerInteractionAuthority` / `PlayerInteractionAuthorityPolicy` lock
   take/heal/use/carry as host-validated no-prediction and push as
   presentation-only; the result journal maps them to kernel `AuthorityKind`.
9. [x] Add carry relation consistency invariant: a carrier must be alive and
   conscious, locked by `DeadCarrier_IsRejectedByInvariant` and
   `UnconsciousButAliveCarrier_IsRejectedByInvariant`.
