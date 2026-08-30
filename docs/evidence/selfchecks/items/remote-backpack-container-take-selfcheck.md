# Remote backpack nested-container take — self-check (2026-08-27)

Closes the open bug "Remote backpack item operations unavailable inside open
containers". The native remote-backpack view already materialised recursive
container contents as display proxies; the missing half was the cross-player
take path for items that are not top-level body slots. This cycle makes the
existing host-authoritative take operation container-depth aware and exposes
it through both the native remote backpack drag surface and the custom Online
UI inventory tree.

## 1. Mechanism inventory (evidence-first)

| # | Mechanism | Evidence |
|---|-----------|----------|
| 1 | Character snapshot item tree | `CharacterItemMsg.Contents` is recursive (`src/.../Protocol/Messages/CharacterItemMsg.cs:38`) |
| 2 | Existing take authority | `PlayerInventoryTakeService.HandleTakeRequest` moved only top-level `source.Items` entries before this cycle |
| 3 | Remote clone display proxies | `CloneInventoryRenderer.RestoreRemoteContents` materialises nested contents with `RemoteCloneRender` + `ItemInstanceId` |
| 4 | Native radial read path | `InvButton.get_body` is routed to `RemoteBackpackView.FocusedBody` while the remote view is open |
| 5 | Native release path | `PlayerCamera.HandleReleaseDragging` is where a dragged item would normally mutate a body — the remote view intercepts it |
| 6 | Native drag loop mutations | `PlayerCamera.HandleWhileDragging` toggles `favourited` on hovered `InvButton` items — must not run on display proxies |
| 7 | Radial re-anchor hazard | `PlayerCamera.HandleTradeMenu` re-anchors `radialMenu` to the local body every frame — would undo the remote-clone anchor |

## 2. Changes

- **Host authority is now recursive** — `PlayerInventoryTakeService` deep-clones
  the character snapshot and removes the requested item from any depth
  (`TryFindAndRemove` walks `Contents` recursively). A taken item becomes a
  top-level slot item on the recipient, exactly like the existing top-level
  take. Worn items remain refused; conscious/alive targets remain refused.
- **Deep-copy the character tree** — `PlayerCharacterAccess.CloneCharacter` /
  `CloneItem` now deep-clone recursive container contents, so a nested removal
  on the host's cloned snapshot can never mutate the live stored snapshot by
  sharing a container list.
- **Local body removal is recursive** — `PlayerInteractionApply.RemoveCarriedItemFromLocalBody`
  searches the entire carried-item subtree (`GetComponentsInChildren<Item>`)
  instead of only direct slot/limb children, so the source participant actually
  loses a nested item when the authoritative transfer arrives.
- **Native remote-backpack take** — the read-only pickup block is removed;
  dragging a `RemoteCloneRender` display proxy from the focused remote clone
  now sends the existing `SendTakeRequest(owner, instanceId)` on release and
  skips the native body-mutation path.
- **Native drag-loop isolation** — `PlayerCamera.HandleWhileDragging` is now a
  prefix that skips the original game body while the remote view is open (the
  original contains display-proxy mutations such as favourite toggles), while
  still keeping the radial anchored to the focused clone. A new
  `PlayerCamera.HandleTradeMenu` prefix prevents the local-body radial re-anchor.
- **Custom Online UI nested take buttons** — recursive inventory entries now
  show a Take button at every container depth in the expanded custom inventory
  tree, using the same `TakeItem` action and host decision surface.
- **No wire change** — no new `NetMsg`, no `ProtocolVersion` bump, no
  event/item/entity matrix row touched.

## 3. Self-check table

| Mechanism | Change | Evidence |
|-----------|--------|----------|
| Nested take reaches host authority | `TryFindAndRemove` recursively removes the requested instance id | `PlayerInteractionServiceTests.Guest_TakesNestedItemFromUnconsciousHostContainer...` (red before fix: no transfer) |
| Deeply nested take removes only deepest parent | Recursive walk preserves outer/inner containers | `...NestedContainerInsideUnconsciousHost_RemovesOnlyFromDeepestParent` |
| Conscious target still refuses nested take | Existing vital gate runs before the recursive search | `Take_NestedItemFromConsciousPlayer_IsRefused` |
| Source local body removes nested item | `RemoveCarriedItemFromLocalBody` searches the full item subtree | Static code path (`PlayerInteractionApply.cs`); no L0 Unity body harness by design |
| Native remote view can take a display proxy | `PlayerCameraDragUsePatch` calls `TryHandleRemoteBackpackTake` before the native release path | `RemoteBackpackContractTests.PatchBridge_ExposesRemoteBackpackTakeSurface` + patch contract resolution |
| Native drag loop cannot mutate a proxy | `PlayerCamera.HandleWhileDragging` prefix skips the original while remote view is open | Static code path; patch contract resolution |
| Radial stays on remote clone | `PlayerCamera.HandleTradeMenu` prefix skips the local re-anchor while open | Static code path; patch contract resolution |
| No wire/event-matrix break | No NetMsg/ProtocolVersion/event-row changes | `git diff` contains only Runtime/GameAdapter/Plugin/tests/docs |

## 4. Verification (development-period, no manual acceptance)

- **Before-red**: the two nested-take tests failed on the pre-fix host authority
  with "sequence contains no matching element" (no transfer message was sent).
- **L0**: `dotnet test CasualtiesUnknownOnline.slnx --no-build` — **1563 passed / 0 failed**.
- **Gates**: `tools/check-architecture.ps1`, `tools/check-event-replay.ps1`,
  `tools/check-entity-event-dispatch.ps1` all pass.
- **Format**: `dotnet format` run.
- **Runtime verification**: development-period rule — L0 simulation + static
  evidence, **no manual acceptance**.

## 5. Structure review

- `PlayerInventoryTakeService` stays under the 600-line gate; it gains one
  recursive helper and one deep-copy path.
- `PlayerCharacterAccess` remains a small pure projection; its clone helpers are
  now recursive but still single-purpose.
- `PlayerInteractionApply` loses loop code and gains one `GetComponentsInChildren`
  search; still under the gate.
- New patch classes are small: `PlayerCameraHandleTradeMenuPatch` is ~20 lines;
  `PlayerCameraDragUsePatch` is extended within the existing type.
- Dead mechanisms: the old `PlayerCameraTryPickupFromUIPatch` read-only pickup
  block is deleted — the read-only guarantee now lives in the release/drag-loop
  isolation instead of blocking the operation the bug needed.
