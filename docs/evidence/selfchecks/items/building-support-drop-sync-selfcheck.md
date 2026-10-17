# Building Support-Loss Drop Sync — quantity + fresh presentation self-check

Delivery-cycle fact sheet for the re-opened
`docs/backlog/done/trap-destruction-drop-quantity-desync.md` and
`docs/backlog/review/entity-destruction-drop-guest-fresh-state-loss.md`.

## 1. Root cause

A `requireGround` building that dies because its support block was removed is
**not** a destructive trap event. The previous `EntityEventMsg.Drops` +
`ApplyTrapDropPresentation` path therefore never runs for this reproduction:

- `EntityEventSync.OnEntityEvent` only holds destructive trap kinds
  (`TrapDamageProfiles`' kind table).
- `BuildingEntity.CheckSeating` is registered as a `WorldGeneration`
  `ChunkUpdated` listener; a remote air-write can set a building's health to 0,
  but the non-breaker side was never marked `RemoteEntityDeath`
  (`BuildingEntityPatches`' death-scope prefix), so every side could roll its own
  random drop set.
- Building-death drops that did not fold into a trap event went through the
  standalone kernel spawn path (`ItemWorldSync`'s `[ItemSpawned]` fallback), which
  at the time carried no `FreshItemDrop`, velocity, rotation or angular velocity.
  The carrier that closed that half is
  `docs/backlog/review/entity-destruction-drop-guest-fresh-state-loss.md`.

## 2. Fix

- **Single drop owner for support-loss deaths**: `WorldEventSync` now marks
  nearby `requireGround` buildings as `RemoteEntityDeath` when a remote
  air-write / block-state snapshot removes the support block
  (`WorldBuildingEntitySync.MarkSupportLossRemote`), so the non-breaker side
  does not roll its own drop set.
- **Building drops ride the same one-message/one-verdict path as the break**:
  `BlockDamagedMsg` gains optional `BuildingDrops` (`TrapDropEntryMsg` list);
  `BlockBreakPendingState` collects them alongside block drops;
  `ItemWorldSync` folds a building-death drop into the pending block break
  before falling back to a standalone spawn. The fold is keyed on the CELL the
  dying building stood on (`buildingDrop.SupportCell` against the pending break's
  own cell, published by the death scope and stamped at `Item.Awake`), so a
  building that died elsewhere inside that window reports standalone instead of
  riding a break whose cell its table does not belong to.
- **Full transient initial state survives to peers**: `BlockDropSync` maps
  `TrapDropEntryMsg` through `InitialDropStateMapper` and fires the same
  `ItemSpawned` application surface, so the receiving side materializes the
  drop with its fresh flag, velocity, rotation and angular velocity.
- **Wire compatibility**: `BlockDamagedMsg` gains `[ProtoMember(5)]`;
  `ProtocolVersion.Current` is bumped 3 → 4 because the wire shape changed.

## 3. Regression coverage

- `BlockBreakPendingStateTests.TryAddBuildingDrop_FoldsIntoThePendingList`
  locks the pending-state building-drop collection.
- `BlockBreakPendingStateTests.TryAddBuildingDrop_WithoutBreak_False` locks
  the fallback-to-standalone behavior.
- `BlockBreakSimulationTests.BuildingDropsRideTheAcceptedBreakRelay` drives a
  real host/guest wire path and verifies the relayed `BlockDamagedMsg` carries
  the building drop with full initial state.

## 4. Verification (development-period, no manual dual-client acceptance)

| Evidence | Result |
|---|---|
| `dotnet build CasualtiesUnknownOnline.slnx` | 0 warnings / 0 errors |
| `dotnet test CasualtiesUnknownOnline.slnx` | 2254 passed / 0 failed |
| `dotnet format CasualtiesUnknownOnline.slnx` | clean |
| `check-architecture.ps1` / `check-event-replay.ps1` / `check-entity-event-dispatch.ps1` | pass |
| `check-delivery.ps1` | pass (filled after backlog update) |
| Deploy identity | `tools/deploy.ps1` deployed to the real game directory; deployed DLL hash matches build output |

## 5. What was NOT changed

- No continuous item physics was added to the kernel.
- No new event kind or trap authority rule was introduced.
- The block-break first-writer-wins arbitration remains unchanged; building
  drops are subject to the same accept/refuse verdict.
