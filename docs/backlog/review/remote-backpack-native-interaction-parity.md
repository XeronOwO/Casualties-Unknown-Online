# Remote backpack native interaction parity

- Status: Review
- Priority: Medium
- Category: Remote inventory / co-op interaction parity
- Source: User report (2026-09-04) — opening another player's backpack only supports a small subset of native backpack interactions. Implemented in the native remote-backpack cycle; selfcheck linked.

## Goal

Make the native remote-backpack view behave like the player's own backpack for the normal inventory interactions that should be valid in co-op, while preserving host authority and never mutating remote display proxies directly.

## Current behavior (after implementation)

The native remote-backpack view is still a presentation-only surface: remote clones are display proxies. Native gestures are now mapped onto host-authoritative semantic operations rather than allowing the original body mutation path to touch a proxy.

Implemented gestures:

1. **Take** (pre-existing) — dragging a display-proxy item out sends the existing host-authoritative take request.
2. **Pour water** — a remote water bottle dragged to the left edge sends a `Pour` operation; the host empties the authoritative liquid stacks and records a participant state result.
3. **Drop at screen edges** — a remote item dragged to the left/right screen boundary sends a `Drop` operation; the host moves the kernel item to World at the owner's position and tells the owner's body to remove it.
4. **Move into a container owned by the remote player** — dropping a remote proxy onto a remote container sends a `MoveToContainer` operation; the host reconciles the container subtree in the kernel and tells the owner's body to place the item into that exact container.
5. **Tab-switch transfer** — holding a remote proxy, closing the remote view, opening the local backpack and releasing into the local inventory reuses the existing host-authoritative take request, using the owner SteamId stamped on the display proxy.

Unsupported native gestures (combine, use, wear, battery load/unload, favorite, slot swap, craft/open windows) are still cancelled before native mutation; the cancellation is logged and the drag is cleared visibly.

## Operation map

| Native remote-backpack gesture | CUO operation | Authority path | Status |
|---|---|---|---|
| Take/drag out | `PlayerInventoryTakeRequest` | `PlayerInventoryTakeService` | Implemented (pre-existing) |
| Pour/dump | `RemoteInventoryOperationRequest` → `Pour` | `PlayerRemoteInventoryService` + `SyncItem/Update/PlayerItemUseResult` | Implemented |
| Edge drop | `RemoteInventoryOperationRequest` → `Drop` | `PlayerRemoteInventoryService` + kernel World relocation + owner-removal transfer | Implemented |
| Move into remote container | `RemoteInventoryOperationRequest` → `MoveToContainer` | `PlayerRemoteInventoryService` + `SyncContainerItemsCommand` + same-owner parent transfer | Implemented |
| Tab-switch transfer | existing take request from proxy owner marker | `PlayerInventoryTakeService` | Implemented |
| Combine / use / wear / load / battery / favorite / slot swap | not mapped | cancelled + log | Unsupported future |
| Cross-player remote-to-remote item handoff without local inventory | not mapped | not implemented | Future |

## Design notes

- The remote proxy marker `RemoteInventoryItemId` now carries both the authoritative instance id and the owner SteamId. This is what makes the Tab-switch path safe after `RemoteBackpackView.Close` no longer cancels an in-progress proxy drag.
- The drag release patch cancels every proxy release that is not consumed by one of the authorized host operations. No proxy is allowed to fall through to the native/local body mutation path.
- All authoritative mutations update the kernel, the owner's saved character snapshot, and the owner's local body through the existing player-interaction result projection. Remote clones update from the owner's immediate re-report.
- `AllowRemoteInventoryTake` remains the host gate for all remote inventory operations; take/transfer additionally keeps the existing unconscious/dead rule.

## Acceptance criteria

- Each reported interaction (pour, edge drop, move to remote container, Tab-switch transfer) produces the same intended authoritative result as the equivalent native backpack operation.
- The complete family is covered by the documented operation map above; unsupported gestures fail visibly by cancellation + log.
- All authoritative mutations are one-operation-one-owner and travel through existing host-authoritative paths; no direct display-proxy mutation was added.
- The remote view remains safe against duplicate/ghost items and closed-view drag escape (`RemoteProxyDragPolicyTests` stay green).
- Unsupported/blocked operations are observable.
- `dotnet build`, `dotnet test`, `dotnet format`, and repo gates pass.

## Evidence

- Selfcheck: `docs/evidence/selfchecks/items/remote-backpack-native-interaction-parity-selfcheck.md`
- Host operation service: `src/.../Runtime/Session/PlayerInteraction/PlayerRemoteInventoryService.cs`
- Drag routing: `src/.../GameAdapter/Patches/PlayerCameraDragUsePatch.cs`
- Bridge routing split: `src/.../GameAdapter/RemoteBackpackOperationHandler.cs`
- Parent transfer carriage: `PlayerInventoryTransferEvent` / `PlayerInventoryTransferMsg.TargetParentItemId`

## Non-goals

- Not merely relaxing the read-only/cancel guard — the supported gestures now have real semantic operations.
- Not inventing arbitrary owner permissions beyond the existing host rules and the normal backpack interaction vocabulary.
- Combine/use/wear and other non-carry inventory actions on a remote proxy are deliberately left as future work; they are documented and observable rather than silently mutating a proxy.
