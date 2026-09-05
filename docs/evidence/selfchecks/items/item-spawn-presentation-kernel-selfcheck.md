# Item spawn presentation through the kernel — self-check

Delivery-cycle fact sheet for the still-open
`docs/backlog/todo/entity-destruction-drop-guest-fresh-state-loss.md` family:
building/entity destruction drops must carry the same fresh-drop highlight,
initial velocity, rotation and angular velocity to every peer, not only to the
destroying side.

## 1. Root cause

Ordinary building/entity death drops (attack or open — not destructive traps,
not support-block removal) left the destroying side as standalone `ItemSpawn`
reports. The kernel `SpawnItemCommand` / `ItemSpawnedEvent` / wire DTOs carried
only the save-shaped item data and world position. The projection on every
receiving side therefore materialized the item with `FreshItemDrop = false`,
zero velocity, zero rotation and zero angular velocity:

- `KernelBatchItemProjection.ToWorldItem(ItemState)` — zero/false projection
- `ItemMessageFlowService.SendItemSpawned` — guest wire command did not include
  the transient initial state
- `ItemProjection.ApplySpawn` / `ApplyRegisterIfAbsent` — spawned the kernel
  fact without the transient metadata

The earlier `EntityEventMsg.Drops` and `BlockDamagedMsg.BuildingDrops` paths
fixed destructive traps and support-loss building deaths, but not ordinary
building deaths. The user's bidirectional rejection — whichever side destroys
an entity, the non-destroying peer lacks the white fresh-drop presentation —
was exactly this missing family.

## 2. Fix

- `SpawnItemCommand` and `ItemSpawnedEvent` now carry optional transient
  presentation fields: `VelocityX`, `VelocityY`, `Rotation`, `FreshItemDrop`,
  `AngularVelocity`.
- `WireCommand` / `WireEvent` carry the same fields (protonumbers 26-30 and
  25-29); `ProtocolVersion.Current` is bumped 9 → 10.
- `ItemMessageFlowService.SendItemSpawned` includes the full initial drop state
  in the guest's item-spawn wire command.
- `ItemProjection.ApplySpawn` / `ApplyRegisterIfAbsent` pass the same transient
  state into the kernel spawn command, so host-originated and guest-originated
  spawns both preserve it through the committed batch.
- `KernelBatchItemProjection` uses the `ItemSpawnedEvent` transient fields when
  projecting a new world item, instead of always projecting zero/false.
- `TrapStateRegistry.ReportBatch` also passes the trap drop's transient fields
  into the kernel spawn command; the existing `EntityEventMsg.Drops`
  presentation path remains as the local-enrichment fallback.
- New `ItemSpawnWireMapper` owns the spawn-family wire mapping so
  `KernelWireMapper` stays under the architecture line gate.

## 3. Regression coverage

- `KernelSpawnPresentationProjectionTests.Spawn_PresentationFlowsThroughKernelBatchProjectionToWorldItem`
  — a kernel spawn with full transient state projects a `WorldItem` with all
  fields preserved.
- `KernelWireMapperTests.BatchRoundTrip_PreservesSpawnPresentationFields` —
  committed-batch wire round-trip preserves velocity/rotation/fresh/angular.
- `ContainerSyncProtocolTests.GuestItemSpawn_CarriesTransientInitialDropStateOnTheWire`
  — a guest's item-spawn wire command actually carries the full initial state.
- `ContainerSyncProtocolTests.GuestItemSpawn_PresentationFlowsThroughHostBroadcastToGuestProjection`
  — end-to-end fake host/guest path: the guest's own projected `WorldItem`
  after the host broadcast has the full initial-drop presentation.

## 4. Verification (development-period, no manual dual-client acceptance)

| Evidence | Result |
|---|---|
| `dotnet build CasualtiesUnknownOnline.slnx` | 0 warnings / 0 errors |
| `dotnet test CasualtiesUnknownOnline.slnx` | 2310 passed / 0 failed |
| `dotnet format CasualtiesUnknownOnline.slnx` | run |
| `check-architecture.ps1` | pass |
| `check-event-replay.ps1` | pass (33 events) |
| `check-entity-event-dispatch.ps1` | pass (33 kinds x 3 tables) |

Runtime acceptance: this is a protocol/projection behavior change. Development
verification proves the wire/projection facts; real dual-client visual
acceptance of the white fresh-drop highlight on the non-destroying peer still
requires the user's final pass.

## 5. What was NOT changed

- No new item-location/authority rule; the kernel still does not own continuous
  item physics.
- No change to block-break, destructive-trap, support-loss or remote-backpack
  item sync paths beyond preserving the already-reported transient spawn state.
- No new message type; the existing item-spawn command/event shape carries the
  transient presentation fields.
