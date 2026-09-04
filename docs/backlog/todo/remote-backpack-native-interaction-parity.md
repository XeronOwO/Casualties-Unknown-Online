# Remote backpack native interaction parity

- Status: Todo
- Priority: Medium
- Category: Remote inventory / co-op interaction parity
- Source: User report (2026-09-04) — opening another player's backpack only supports a small subset of native backpack interactions. Record only; no code action taken yet.

## Goal

Make the native remote-backpack view behave like the player's own backpack for the normal inventory interactions that should be valid in co-op, while preserving host authority and never mutating remote display proxies directly.

## Current behavior

The native remote-backpack view is intentionally a read-only presentation surface except for one operation: dragging a display-proxy item out and releasing sends a host-authoritative remote **take** request.

Evidence:

- `src/CasualtiesUnknownOnline.GameAdapter/RemoteBackpackView.cs:5-12` — the focus is presentation-only; remote clones are display proxies, so the native UI "must never be allowed to mutate the focused clone".
- `src/CasualtiesUnknownOnline.GameAdapter/Character/RemoteInventoryItemId.cs` — after the id-free display boundary, remote clone items no longer carry domain `ItemInstanceId`; this display-only marker carries the authoritative instance id for the remote-backpack gesture path without re-introducing domain lookup ambiguity. The take path reads this marker.
- `src/CasualtiesUnknownOnline.GameAdapter/Patches/PlayerCameraDragUsePatch.cs:21-39` — while the remote view is open, only `TryHandleRemoteBackpackTake` is allowed; any other dragged item released during the remote view is cancelled.
- `src/CasualtiesUnknownOnline.GameAdapter/GameAdapterBridge.cs:193-256` — the only implemented release outcome is a cross-player take request; `CancelRemoteProxyDrag` handles every other display-proxy release.
- `src/CasualtiesUnknownOnline.GameAdapter/RemoteProxyDragPolicy.cs` — any remote display-proxy release that is not consumed by remote take must be cancelled.
- `docs/evidence/selfchecks/items/remote-backpack-container-take-selfcheck.md` — the current supported native backpack interaction after this slice is recursive take (including drag release from the native remote view).
- `docs/evidence/selfchecks/players/native-remote-backpack-and-door-sound-selfcheck.md` — describes the native remote backpack view as the replacement for the old custom UI; the custom item-list detail fallback was later removed on 2026-09-04.

## Reported missing interactions

User-visible cases that are normal backpack operations but are not implemented in the remote view:

1. **Pour water** — drag a remote player's water bottle toward the left side of the screen to pour/dump the liquid.
2. **Drop at screen edges** — drag the item to the left/right screen boundary to drop it.
3. **Move into a container owned by the remote player** — e.g. move his water bottle into his trash bag.
4. **Switch backpack via Tab** — hold the remote player's item, press Tab twice to close his backpack, open the local player's backpack, and transfer the held item into the local backpack.
5. **Other unmentioned native backpack gestures** — the user explicitly notes there are likely more: combine, use, load/unload, transfer between slots/containers, and any other operation the game's own backpack UI supports.

These are not missing because the user is asking for a niche feature; they are the normal interaction vocabulary of the game's inventory UI.

## Likely root cause

The remote backpack reused the native radial UI by pointing it at a remote render clone. The remote clone's item objects are display proxies (`RemoteCloneRender` + `ItemInstanceId`), so the original native release/drop/container/slot logic cannot run on them without either:

- mutating a non-authoritative proxy on the local side, or
- sending a host-authoritative semantic operation to the owner's authoritative inventory and then projecting the result back to every side.

Currently only the "take to my inventory" mapping has a real semantic operation; all other native gestures are either swallowed/cancelled or fall through to the local body path which is intentionally blocked.

## Required design direction (for the implementation cycle)

Before implementing, inventory the complete native backpack interaction vocabulary and map each gesture to an authoritative operation:

- Reuse existing kernel commands where they already model the fact:
  - `DropItemCommand`, `TransferItemCommand`, `UpdateItemStateCommand`, `SyncContainerItemsCommand`, `CookItemCommand`, carry/player inventory commands in the typed kernel.
- Determine which operations can be initiated by the local viewer on a remote player's inventory, and what authority/consent rules apply:
  - Take is already host-validated (unconscious/dead only, with `AllowRemoteInventoryTake`).
  - Pour/drop/container moves are inventory mutations on the owner; they need an explicit host-authoritative request/result path or a mutual/semantic policy, not direct local mutation.
- The native gestures that involve the local player's own backpack (Tab switch, transfer into own backpack) are a cross-player transfer/move and must be one atomic operation.
- Ensure every accepted mutation updates:
  - the item kernel / host authority,
  - the owner's local body items,
  - all remote clone displays and the Online UI/quick panel fallbacks (if kept),
  - reconnect/save restore paths.
- Keep the existing display-proxy isolation invariants:
  - never mutate `RemoteCloneRender` items as if they were authoritative,
  - no duplicate item ghost between remote and local inventories,
  - cancelled/unsupported gestures must fail visibly (log + UI feedback) instead of silently becoming no-ops or leaking into the local body.

## Acceptance criteria (for the later implementation cycle)

- Each reported interaction (pour, edge drop, move to remote container, Tab-switch transfer) produces the same intended authoritative result as the equivalent single-player/native backpack operation.
- The complete family of native backpack gestures is covered by a documented operation map, not just the four examples.
- All authoritative mutations are one-operation-one-owner and travel through the existing host-authoritative paths; no direct display-proxy mutation is added.
- The remote view remains safe against duplicate/ghost items and closed-view drag escape (existing `RemoteProxyDragPolicyTests` stay green).
- Unsupported/blocked operations are observable (logs, no silent failure).
- `dotnet build`, `dotnet test`, `dotnet format`, and repo gates pass.

## Non-goals

- Not merely relaxing the read-only/cancel guard — the point is to add real semantic support for these operations.
- Not inventing arbitrary owner permissions beyond what the game's normal backpack semantics require; follow existing host-authority and `HostRules` patterns.
- Not implementing in this cycle — this ticket is a backlog record only.
