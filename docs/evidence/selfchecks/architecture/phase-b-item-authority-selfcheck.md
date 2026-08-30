# Phase B item authority self-check (2026-08-28)

This fact sheet records the Phase B authority switch: the typed deterministic
kernel now owns the persistent item facts, the legacy tables are projections,
the native-operation and capability surfaces are introduced, and the item
checkpoint path exists behind a temporary in-memory store.

## Mechanism inventory

| Mechanism | Where | Notes |
|---|---|---|
| Full kernel item payload | `src/CasualtiesUnknownOnline.GameState/Domains/Items/ItemData.cs`, `ItemLiquidStack.cs`, `ItemComponentState.cs`, `ItemComponentField.cs` | condition, favourited, slot, liquid stacks, typed component fields; no Protocol DTOs. |
| Kernel command surface | `SpawnItemCommand`, `PickUpItemCommand`, `DropItemCommand`, `DestroyItemCommand`, `UpdateItemStateCommand`, `TransferItemCommand` | all item facts enter as typed commands. |
| Item domain reducers/invariants | `ItemDomainModule.cs` | data payload, contained parent checks, container cycle detection. |
| Checkpoint | `GameCheckpoint`, `ItemKernelAuthority.CreateCheckpoint/Restore`, `ItemCheckpointStore` | in-memory temporary seam until Phase C save format. |
| Host authority service | `Runtime/Session/Items/ItemKernelAuthority.cs` | owns the deterministic kernel, epoch, operation ids, wire->kernel conversion, container subtree sync. |
| World-item projection | `ItemProjection.cs` | only code path allowed to write `WorldItemTable`; every write follows an accepted kernel command (except authorized stream refresh/reset). |
| Carried transfer projection | `ItemArbitration.cs` | methods call `ItemKernelAuthority` before mutating the transfer cache; container-content reports sync kernel child items. |
| Native operation coordinator | `GameAdapter/Items/NativeOperationCoordinator.cs` + `NativeObservation`, `NativeOperationHandle` | Begin/Observe/Complete/Abort, one observation, remote-apply suppression, abort/run reset. |
| Item capability registry | `GameAdapter/Items/ItemCapabilityRegistry.cs` + `SavedStateItemCapability`, `LiquidItemCapability`, `GunItemCapability`, `CustomDataItemCapability` | five required surfaces: Capture, Restore, Equivalent, Validate, Presentation. |
| Write-path gate | `tools/check-item-authority.ps1` | rejects direct `WorldItemTable` / transfer-table mutations outside the projection classes; wired into `check-architecture.ps1`. |
| Wire-free guard | `tools/check-gamestate-isolation.ps1` | extended with Protocol DTO/protobuf/net-vector token checks. |

## Evidence table

| Claim | Evidence |
|---|---|
| The kernel stores the full save-shaped item payload | `GameStateItemDataTests`, `ItemDomainModule.Reduce(ItemSpawnedEvent/ItemDataUpdatedEvent)`. |
| Update/transfer/container commands are deterministic and reject stale revisions | `GameStateItemDataTests`, `GameStateKernelTests`, `ItemDomainInvariantTests`. |
| Old world table writes are gated by kernel acceptance | `tools/check-item-authority.ps1`; all world-table mutations live in `ItemProjection.cs`. |
| Carried action reports update the kernel before the transfer cache | `ItemArbitration.AdoptEvidence`, `RecordSlot`, `RecordContainerContent`, `RegisterCarried`, `AdoptTransferredItem`, `UpdateTransferredItem`. |
| Container contents become authoritative kernel child items | `ItemKernelAuthority.SyncContainerContents`, `ItemContainerSyncTests`. |
| Native operations produce exactly one observation and no remote-apply echo | `NativeOperationCoordinatorTests`. |
| Capability registry rejects partial/duplicate capabilities | `ItemCapabilityRegistryTests`. |
| Item checkpoint round-trips through the same reducer | `ItemCheckpointStoreTests`, `GameStateItemDataTests.CheckpointRoundTrip`. |
| Existing multiplayer behavior is preserved | Full suite (1611 green), item/replay tests green, no new wire message introduced. |

## Verification

- `dotnet build CasualtiesUnknownOnline.slnx`: clean, 0 warnings/errors.
- `dotnet test CasualtiesUnknownOnline.slnx`: all tests pass.
- `tools/check-architecture.ps1`: passed (includes GameState isolation + item authority).
- `tools/check-event-replay.ps1` and `tools/check-entity-event-dispatch.ps1`: run with the delivery gate.
- Replay differential: all item `.replay` files still produce zero kernel semantic diff.
- No production wire/protocol/save change; old wire DTOs remain as presentation projections until Phase C.

## Structure review

- GameState remains dependency-free and deterministic; the expanded item model is
  typed and contains no wire DTOs.
- `ItemKernelAuthority` is the single kernel writer; `ItemProjection` is the single
  legacy world-table writer; `ItemArbitration` is the transfer cache projection and
  calls authority before every cache mutation.
- No new class exceeds the 600-line gate; all new files are one top-level type.
- The capability registry and native coordinator are self-contained adapter
  surfaces with tests, not yet fully absorbed into every Harmony patch site
  (recorded as a Phase C follow-up).
- Historical race/replay tests were updated only where the oracle did not model
  kernel terminal semantics; the delivery order and reject streams still match.
