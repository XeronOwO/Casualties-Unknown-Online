# Remote backpack native interaction parity

- Status: In Progress (critical / super-priority; code-complete, final deployment deferred by user request while playing)
- Priority: Critical
- Category: Remote inventory / co-op interaction parity / container sync
- Source: User report (2026-09-04); rejected by user (2026-09-05) — opening another player's backpack still could not perform most item operations; rejected again as a whole (2026-09-05) on container/trash-bag interaction; user now reports the remote-backpack operation problem still exists with behavior identical to before any fix and marks it a super-priority issue.

## Rejected again (latest user re-report — super priority)

The user reports the problem of operating on another player's backpack items
still exists and has been sent back many times. The observed behavior is
described as **exactly the same as before the fix**, with no visible
improvement. This ticket is marked **Critical / super-priority** and moved back
to open todo. All "Landed"/implementation notes below are not accepted as
complete: the exact user reproduction must be re-tested on deployed artifacts
until remote-backpack operations work as intended.

## Rejected as a whole (2026-09-05 user re-test)

User re-tested opening another player's backpack and using container/trash-bag
interaction. The behavior is not accepted:

- Dragging a water bottle from the remote backpack into a trash bag does not
  enter immediately; it only appears to be placed through the periodic sync /
  fallback mechanism.
- After dragging the bottle in, opening the trash bag can show the water bottle;
  after the next periodic sync the bottle disappears from the container view.
  Re-opening the trash bag repeats the visible → invisible cycle.
- The trash bag's weight/mass display has intermittent jumps; it appears to
  double for one frame at times.
- Quickly trying to take the water bottle back out of the trash bag: the item can
  be dragged, but cannot actually be removed.
- The whole remote-backpack native interaction feature is rejected and must be
  redone/fixed until these container paths work immediately, stay stable through
  periodic sync, and allow take-out.

## Autonomous fix cycle (2026-09-05)

The container/trash-bag symptoms traced to two concrete gaps in the display/fact
chain:

1. **Carried-fact moves did not keep the fact tree exact.** A container move
   event replaced the destination container in `CloneFactTable` but left the
   moved child in its old top-level/nested position until the next full
   snapshot. This produced duplicate views, the divergence warning, and the
   visible→invisible instability.
2. **The remote clone container renderer rebuilt all children on every
   snapshot/event.** It destroyed existing proxy children before the native open
   container window could refresh, so buttons kept references to destroyed
   items; it also briefly showed old+new children (double weight), and nested
   containers with contents were rejected by the native `Container.LoadItem`
   stacking guard.

Fixes landed in this cycle:

- `CloneFactTable.ApplyCarriedSync` now prunes moved descendants from their old
  positions and, when an item-level event carries a real slot/limb home, moves
  the item from any nested container to the top-level fact list. `RemoveNested`
  now removes only the target item, never an ancestor container.
- `CloneInventoryRenderer` now reconciles container children incrementally by
  direct-child/instance-id matching, unloads removed children before destroy
  (no double weight), hides them immediately after unload (no ghost), and
  manually parents nested containers with contents (bypassing the stacking
  guard). Slot/limb rendering matches only direct children, preventing
  same-item-id nested children from being destroyed as duplicates.
- `RemoteBackpackView` now tracks the open remote container by authoritative
  instance id, and `RemoteBackpackCoordinator` / the renderer's removal paths
  schedule a one-frame open-container repopulate/re-bind whenever the open
  container (or an ancestor/limb container) is replaced, so the native window
  never keeps buttons to destroyed proxies.

Regression coverage: 3 new `CloneFactTable` cases (move into container,
move out of container, deep move out keeping ancestors, worn move).
`dotnet test` full suite: **2317 passed / 0 failed**. Architecture,
event-replay, entity-event-dispatch, and delivery gates all pass.

**Deployment status**: the final DLLs are NOT deployed to the game because the
user asked to stop deploying while playing. The previous deployed build is the
pre-fix build; do not use the current game copy for acceptance until this code
is deployed after the session.

## Goal

Make the native remote-backpack view behave like the player's own backpack for
the normal inventory interactions that are valid in co-op, including combine,
use, wear, battery load/unload, favourite, slot move/swap, craft/container
windows, while preserving host authority and never mutating remote display
proxies directly.

## Outcome

The native remote-backpack view now routes every named native gesture to a
host-validated operation. Operations that mutate the owner's real inventory are
validated by the host and sent to the owner's own client as a
`RemoteInventoryApplyMsg`; the owner executes the exact native body/item
operation on its real local body. Existing sync paths (item use, slot move,
craft report, character snapshot) carry the result back to the host and to every
peer's clone, so the remote display proxies are still never mutated locally.

## Operation map

| Native remote-backpack gesture | CUO path | Authority | Status |
|---|---|---|---|
| Drag item out / release (take) | `PlayerInventoryTakeRequest` | Host validates unconscious/dead + `AllowRemoteInventoryTake` | Implemented |
| Drag to remote container | `RemoteInventoryOperationRequest → MoveToContainer` | Host state-based operation | Implemented |
| Drag to left/right edge | `RemoteInventoryOperationRequest → Drop` | Host state-based operation | Implemented |
| Drag remote water container to left edge / drain | `RemoteInventoryOperationRequest → Pour` | Host state-based operation | Implemented |
| Drag to radial centre (use/wear) | `RemoteInventoryOperationRequest → Use/Wear` → `RemoteInventoryApplyMsg` | Host validates; owner client executes native `Body.UseItem` / `Body.WearWearable` | Implemented |
| Drag one remote item onto another (combine) | `RemoteInventoryOperationRequest → Combine` → `RemoteInventoryApplyMsg` | Host validates second item; owner client executes native `Body.CombineItems` | Implemented |
| Drag battery into/out of battery-powered item | `RemoteInventoryOperationRequest → BatteryLoad/BatteryUnload` → `RemoteInventoryApplyMsg` | Host validates second item; owner client executes native `BatteryItem` APIs | Implemented |
| Slot move / swap | `RemoteInventoryOperationRequest → MoveToSlot` → `RemoteInventoryApplyMsg` | Host validates slot; owner client executes native slot move/swap | Implemented |
| Favourite toggle | `RemoteInventoryOperationRequest → FavoriteToggle` → `RemoteInventoryApplyMsg` | Host validates item; owner client toggles native `favourited` | Implemented |
| Container window open | UI-only remote-proxy gesture (`camera.OpenContainer`) | No authority change; display proxy is never mutated | Implemented |
| Craft screen / see recipes with remote item | UI-only remote-proxy gesture (`camera.OpenCraftScreen` + `SeeRecipesWithItem`) | No authority change | Implemented |
| Tab-switch transfer | existing take request from proxy owner marker | Host validates take rules | Implemented |
| Cross-player remote-to-remote item handoff without local inventory | not mapped | not implemented | Future (out of scope) |

## Design notes

- The previous implementation's release handler always consumed a remote-proxy
  release as a take before any other gesture could be considered. The routing
  order is now: UI-only windows → container move → radial centre use/wear →
  inventory-button battery/combine/slot → pour → edge drop → take fallback.
- New host→owner `RemoteInventoryApplyMsg` carries the validated operation plus
  the primary/secondary item ids and the target slot. The owner's Game Adapter
  resolves the real local items by instance id and calls the exact native body
  methods. It deliberately does not compute a mirrored result in the Runtime:
  the game's own use/wear/combine/battery/slot semantics are the parity target.
- `AllowRemoteInventoryTake` remains the host gate for all remote inventory
  operations; take additionally keeps the existing unconscious/dead rule.
- The remote proxy marker `RemoteInventoryItemId` continues to carry both the
  authoritative instance id and the owner SteamId; Tab-switch transfer is safe
  after `RemoteBackpackView.Close`.
- UI-only container/craft gestures are consumed by the patch without hitting the
  native release path, so a remote container proxy can never be unloaded/dropped
  by the original world-action fallback.
- `IRemoteBackpackPatchBridge` was split from `IPatchBridge` to keep the patch
  bridge under the 600-line architecture gate as the gesture family grows.

## Acceptance criteria

- Each reported interaction (pour, edge drop, move to remote container, Tab-switch
  transfer, combine, use, wear, battery, favourite, slot move/swap, container
  window, craft screen) produces the same intended native result while preserving
  host authority.
- The complete family is covered by the operation map above.
- All authoritative mutations are one-operation-one-owner and travel through the
  host-validated request path; no direct display-proxy mutation was added.
- The remote view remains safe against duplicate/ghost items and closed-view drag
  escape (`RemoteProxyDragPolicyTests` stay green).
- Blocked/unknown operations remain observable by cancellation + log.
- `dotnet build`, `dotnet test`, `dotnet format`, and repo gates pass.

## Evidence

- Selfcheck: `docs/evidence/selfchecks/items/remote-backpack-native-interaction-parity-selfcheck.md`
- Host service: `src/.../Runtime/Session/PlayerInteraction/PlayerRemoteInventoryService.cs`
- Drag routing: `src/.../GameAdapter/Patches/PlayerCameraDragUsePatch.cs`
- Bridge: `src/.../GameAdapter/IRemoteBackpackPatchBridge.cs` + `RemoteBackpackOperationHandler.cs`
- Apply side: `src/.../GameAdapter/RemoteInventoryOperationApply.cs`
- Owner receive path: `src/.../Runtime/Session/Handlers/RemoteInventoryApplyHandler.cs`
- Protocol: `RemoteInventoryApplyMsg.cs`, `RemoteInventoryOperationKind.cs`

## Non-goals

- Not inventing arbitrary owner permissions beyond the existing host rules and
  the normal backpack interaction vocabulary.
- Cross-player remote-to-remote item handoff without a local inventory remains a
  separate future concern.
