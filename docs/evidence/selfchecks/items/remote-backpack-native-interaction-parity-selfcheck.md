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
