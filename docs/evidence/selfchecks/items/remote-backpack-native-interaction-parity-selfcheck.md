# Remote backpack native interaction parity — self-check

> **Status: Current.** This selfcheck supersedes the rejected 2026-09-05 slice.
> The native remote-backpack view now reaches full normal-inventory parity:
> take/drop/pour/container and the previously missing combine/use/wear/battery/
> favourite/slot move/swap/craft/container-window gestures all work through
> host-validated requests and never mutate a remote display proxy.

## 1. Mechanism inventory (evidence-first)

| # | Mechanism | Evidence |
|---|-----------|----------|
| 1 | Native radial inventory uses `InvButton.body` | `InvButtonBodyPatch` routes `InvButton.body` to `RemoteBackpackView.FocusedBody` while open |
| 2 | Display proxy identity marker | `RemoteInventoryItemId` carries both the authoritative instance id and the owner SteamId; `CloneInventoryRenderer.SetRemoteInventoryItemId` writes both |
| 3 | Release handling | `PlayerCameraDragUsePatch` intercepts `HandleReleaseDragging`; named gestures are routed before the take fallback |
| 4 | UI-only remote windows | `TryHandleRemoteUiOnlyGesture` opens the native container/craft windows on a display proxy without letting the original release path unload it |
| 5 | Host operation authority | `PlayerRemoteInventoryService` validates owner/requester/line-of-sight, item existence and operation-specific target/slot; sends `RemoteInventoryApplyMsg` to the owner |
| 6 | Owner-side native apply | `RemoteInventoryOperationApply` resolves real local items by instance id and calls `Body.UseItem` / `Body.WearWearable` / `Body.CombineItems` / battery APIs / slot move / favourite toggle |
| 7 | Existing sync back to host | Native use/slot/craft/character-data flows carry the authoritative result to the host and all peers; battery/favourite additionally send the affected item's wire state |
| 8 | Proxy safety | Unconsumed remote-proxy releases are still cancelled before native/cross-player release logic can move them into an authoritative body |
| 9 | Patch bridge split | `IRemoteBackpackPatchBridge` owns the remote-backpack bridge methods; `IPatchBridge` stays under the 600-line architecture gate |

## 2. Native gesture → host-authoritative operation map

| Native remote-backpack gesture | CUO path | Supported |
|---|---|---|
| Drag item out / release (take) | `PlayerInventoryTakeRequest` | Yes |
| Drag to remote container | `RemoteInventoryOperationRequest → MoveToContainer` | Yes |
| Drag to left/right edge | `RemoteInventoryOperationRequest → Drop` | Yes |
| Drag remote water container to edge/liquid area | `RemoteInventoryOperationRequest → Pour` | Yes |
| Radial centre use/wear | `RemoteInventoryOperationRequest → Use/Wear` → `RemoteInventoryApplyMsg` | Yes |
| Combine two remote items | `RemoteInventoryOperationRequest → Combine` → `RemoteInventoryApplyMsg` | Yes |
| Battery load/unload | `RemoteInventoryOperationRequest → BatteryLoad/BatteryUnload` → `RemoteInventoryApplyMsg` | Yes |
| Favourite toggle | `RemoteInventoryOperationRequest → FavoriteToggle` → `RemoteInventoryApplyMsg` | Yes |
| Slot move/swap | `RemoteInventoryOperationRequest → MoveToSlot` → `RemoteInventoryApplyMsg` | Yes |
| Container window open | UI-only `camera.OpenContainer` | Yes |
| Craft screen / see recipes with remote item | UI-only `camera.OpenCraftScreen` + `SeeRecipesWithItem` | Yes |
| Tab-switch transfer | existing take request from marker owner | Yes |
| Cross-player remote-to-remote handoff without local inventory | not mapped | No (future) |

## 3. Changes

- **Protocol**: `RemoteInventoryOperationKind` extended with `Combine`, `Use`,
  `Wear`, `BatteryLoad`, `BatteryUnload`, `FavoriteToggle`, `MoveToSlot`;
  `RemoteInventoryOperationRequestMsg` adds `TargetItemInstanceId` and
  `TargetSlotIndex`; new `RemoteInventoryApplyMsg` + `NetMsg.RemoteInventoryApply`
  + `RemoteInventoryApplyHandler` (host → owner).
- **Runtime host service**: `PlayerRemoteInventoryService` validates the new
  operations and forwards them as an owner-side native apply; it keeps the
  existing state-based Drop/MoveToContainer/Pour path.
- **GameAdapter**: new `RemoteInventoryOperationApply` runs the exact native
  operation on the owner's real body, then triggers the immediate character
  re-report and sends affected-item state for battery/favourite.
- **Bridge**: `IRemoteBackpackPatchBridge` carries the expanded remote-backpack
  bridge surface; `RemoteBackpackOperationHandler` builds the new requests.
- **Drag routing**: `PlayerCameraDragUsePatch` no longer swallows every remote
  release as a take; named UI gestures are matched first.
- **Favourite key**: `PlayerCameraHandleWhileDraggingPatch` sends the favourite
  request while a remote proxy is hovered.

## 4. Verification (development-period, no manual acceptance)

- **L0 tests added**: host-owner native use raises the owner apply; guest-owner
  native use is sent as `RemoteInventoryApply`; missing combine target is
  refused without an apply; direction classification and bridge contract updated.
- **Full suite**: `dotnet test CasualtiesUnknownOnline.slnx` — **2287 passed /
  0 failed**.
- **Gates**: `check-architecture.ps1`, `check-event-replay.ps1`,
  `check-entity-event-dispatch.ps1` pass; `dotnet format` run.
- **Format**: build is warnings-as-errors clean.

## 5. Structure review

- New top-level types are single-purpose: `RemoteInventoryOperationApply`
  (~220 lines), `IRemoteBackpackPatchBridge`, request/apply DTOs, one handler.
- `PlayerCameraDragUsePatch` (~360 lines) and `PlayerRemoteInventoryService`
  (~520 lines) remain under the architecture gate.
- No display-proxy mutation was added: every inventory-mutating gesture goes
  through the host-validated owner-side native execution path.

## 6. Container/nested apply parity follow-up (2026-09-05)

The rejected container/trash-bag re-test exposed the owner-side apply's
direct-slot-only lookup family. The kernel/host tree is recursive, but the
GameAdapter's local-body item lookup and slot apply still assumed items live
only in direct slot/limb children. That made a nested container source invisible
to the owner apply (a move-to-slot was skipped), made a nested destination
parent invisible to the same-owner container transfer (local add fell back to a
slot), and left `MoveToSlot` unable to physically unload an item out of a
container.

### Root cause

- `RemoteInventoryOperationApply.FindCarriedItemById` searched only
  `body.slots`/`body.limbs` direct children, so a water bottle inside a trash
  bag or any deeper container was not found and every nested remote apply was
  skipped with "item not found".
- `PlayerInteractionApply.FindCarriedItemById` had the same direct-child limit,
  so a same-owner `MoveToContainer` whose destination was a nested container
  could not find the parent and fell back to placing the item in a slot — the
  owner's next 1 Hz snapshot then disagreed with the event-driven clone view.
- `ApplyMoveToSlot` only handled direct slot/limb sources; a container source
  went straight to `Body.PickUpItem` without the game's own
  `Container.UnloadItem` step, so the item could not actually be removed.
- Same-owner container moves were applied as `Destroy` + `RestoreContent` on
  the owner body. Unity's deferred `Destroy` left the old object alive for the
  rest of the frame, so the immediate character re-report and the native
  container weight/display could both see two children with the same instance
  id — the observed one-frame double and the visible→invisible cycle.

### Changes

- New `CarriedItemLocator.FindById` searches the entire local-body carried
  subtree (including recursive container contents) and deliberately skips
  remote display proxies. Both `RemoteInventoryOperationApply` and
  `PlayerInteractionApply` now use this single recursive lookup, eliminating the
  duplicated direct-child search family.
- `RemoteInventoryOperationApply.ApplyMoveToSlot` now detects a container
  parent and calls `Container.UnloadItem` before `Body.PickUpItem`, matching the
  native drag-to-slot path. If the destination slot is occupied and the source
  came from a container, the occupying slot item is loaded into that container
  first (the native swap direction); if the container cannot accept it, the
  operation is refused before the source is unloaded, so the item never becomes
  an unsynchronized orphan. The slot apply intentionally keeps the existing
  native pickup report (no `RemoteApply` suppression): the guest's normal
  `ItemPickup` command relocates the item from `Contained` to `Carried` in the
  kernel, matching the original owner-side apply design.
- `PlayerInteractionApply.OnPlayerInventoryTransfer` now uses a
  same-owner fast path: for a same-owner container move it re-homes the existing
  real item with `Container.UnloadItem`/`LoadItem` instead of Destroy+rebuild,
  leaving no duplicate instance-id child in the same frame. The old transfer
  path remains as the fallback for cross-player transfers and unresolved
  containers.
- `PlayerInteractionApply.AddCarriedItemToLocalContainer` now resolves nested
  destination parents through the same recursive locator; same-owner container
  moves no longer fall back to a slot when the trash bag/box is nested.
- The heal/use local-item consume paths and the local heal-item selector now
  use the recursive locator/full-subtree scan, closing the same family for
  items inside containers.

### Regression coverage

- `Guest_MovesRemotePlayersItemIntoNestedRemoteContainer_UpdatesDeepTreeAndSendsParentTransfer`
  — host/kernel tree and transfer target are recursive-aware.
- `Guest_MovesNestedRemoteItemToSlot_RaisesOwnerApplyWithRecursiveSource`
  — a nested source routes through the host-validated owner apply with the
  correct item id and target slot.

### Verification (development-period, no manual acceptance)

- **Full suite**: `dotnet test CasualtiesUnknownOnline.slnx` — **2312 passed /
  0 failed**.
- **Gates**: `check-architecture.ps1`, `check-event-replay.ps1`,
  `check-entity-event-dispatch.ps1` pass; `dotnet format` run.
- **Runtime verification boundary**: L0 cannot drive the real Unity body; the
  nested-container/container-window behavior still needs the user's final
  dual-client acceptance. The adapter now has runtime evidence points
  (`RemoteApply` logs, `ReportInventoryChanged` immediate re-report) and the
  previously missing local-body path is present in code.
