# Local Item Released Onto A Remote Display Proxy — Self-Check (2026-10-06)

Delivery fact sheet for `done/local-item-into-remote-display.md`, the defect batch `20261006-d` found while it
attributed batch `20261006-c`'s local control. On the pre-fix tree the owner's release of its OWN carried bag onto
a ring slot showing the other player's bag loaded the bag's children INTO the display proxy
(`m1-host-release-onto-proxy.json`: `castItemProxy: true`, `castItemOwner: 76561198863287957`,
`castContentsAfter: 1`, `childInCast: true`); the clone rebuild then unloaded the child into the world and the game
destroyed it 11 ms after the drop report. The player-visible outcome is item loss.

## 1. Mechanism inventory

| # | Mechanism | Evidence |
|---|---|---|
| 1 | The miss is one-sided. The release bracket opens only when the DRAGGED item is a display proxy, so with a locally dragged item no bracket is open and every mutation seam's `window.IsOpen` guard lets the native body run. | `Patches/PlayerCameraDragUsePatch.cs` local branch (`dragItem.GetComponent<RemoteCloneRender>() == null`); every prefix in `Patches/RemoteDragMutationPatches.cs` returns early on `!window.IsOpen` |
| 2 | The native release body resolves its TARGET from the raycast list and `currentContainer`, and its inventory branch mutates that target. | `reversing/Assembly-CSharp/Assembly-CSharp/PlayerCamera.cs:1532` (`TryGetComponent<InvButton>` + `Overlaps`), `:1540` (`invButton.GetItem()`), `:1555-1598` (the container branch and R5's per-child loop), `:1666-1672` (`ContainerBack` → `currentContainer`), `:1541-1553` (battery), `:1600-1604` (combine), `:1605-1631` (body slot) |
| 3 | `InvButton.GetItem()` reads the FOCUSED body while the remote view is open, so the target of a release during that view is another player's proxy item. | `reversing/…/InvButton.cs:180-187` (`isBody` → `body.GetItem(slot)`, otherwise `refItem`) with `Patches/InvButtonBodyPatch.cs` redirecting `InvButton.get_body` to `RemoteBackpackView.FocusedBody`; the container window's own entries carry the proxy container's children as `refItem` (`PlayerCamera.RepopulateContainer`) |
| 4 | Loading this client's own item into a proxy is not a cross-player move: `Container.LoadItem` re-parents the item into the proxy on THIS client, and the clone rebuild then treats the foreign child as stale and drops it. | `reversing/…/Container.cs:116-151` (`item.transform.SetParent(base.transform)`); `CloneInventoryRenderer.MarkRemoteCloneTree` is what makes the newly loaded child a clone object at all (it adds `RemoteCloneRender`, `rb.simulated = false`, `col.enabled = false`), so the NEXT rebuild's removal pass unloads it and `Object.Destroy`s it, and the pending drop report settles onto the position recorded when it LEFT its own container — which is the log arithmetic batch `20261006-d` reads (`[ContainerUnload] dogfood (id …) left its container into the world from the CarriedInventory side`, `[ItemDropped]`, `[ItemDestroyed]` 11 ms later) |
| 5 | The mutation seams cannot carry this fix. R5's expansion unloads each child out of the DRAGGED item's OWN container before loading it into the target, and that first half has no proxy operand — a guard at the load seam alone would let the unload run and dump the children into the world, which is a half-applied gesture of its own. Only cancelling the whole release leaves no half-state. | `reversing/…/PlayerCamera.cs:1589-1590` (`this.dragItem.container.UnloadItem(item3, null)` then `container.LoadItem(item3)`); `Container.UnloadItem` only acts on a direct child of the container it is called on (`reversing/…/Container.cs:154-169`), which is why the R4 unload of a local item onto a proxy is a no-op and the load is the whole mutation |
| 6 | The third target reads no item: a BODY slot of the displayed clone. `isBody` is `slot >= 0`, the ring answers every button's `get_body` from the clone exactly while the view is open, and the native R8/R9 sequence then runs `HoldingItem(int)`, `GetItem(int)`, `SwapSlots` and `DropItem(int)` on the LOCAL body with the CLONE's slot index — so it swaps, or drops into the world, items of this client's own body the player never aimed at. | `reversing/…/InvButton.cs:22-28` (`isBody`), `Patches/InvButtonBodyPatch.cs`, `reversing/…/PlayerCamera.cs:1614-1631` |
| 7 | The world fallback is not READ by the guard, and the reason is narrower than "proxies have no colliders": the two paths that CREATE a clone object disable the collider (`CloneInventoryRenderer`'s materialization pass, and `MarkRemoteCloneTree`'s pass over the children), while the reuse path keeps an existing child untouched. A prefab-leftover proxy is therefore not covered by that argument and is not read here. | `Character/CloneInventoryRenderer.cs` — `col.enabled = false; // never pickable/blocking` in the materialization pass and in `MarkRemoteCloneTree`'s child loop, against `matches[0]` reuse (`if (matches[0].GetComponent<RemoteCloneRender>() == null) AddComponent<RemoteCloneRender>()`, no collider write) and the "template leftover + render" comment beside it; `reversing/…/PlayerCamera.cs:1702-1709` (`Physics2D.OverlapPoint`) — the residual is §4 |
| 8 | The cancel rides the landed fail-closed idiom of the same method: clearing the drag makes the native body take its own "no drag item" early-out, so nothing native runs at all and the item stays where it is. | `PlayerCameraDragUsePatch.Prefix`'s existing unresolved-proxy branch + `reversing/…/PlayerCamera.cs:1459-1466` (`if (!this.dragItem) { … return; }`) |
| 9 | The while-dragging frame's own proxy write had the same one-sided shape: the favourite snapshot ran only while the view was open, and the focus can be cleared while the game still shows the container window it opened (the window closes when the ring has shrunk, `PlayerCamera.HandleRadialMenu`). | `Patches/PlayerCameraHandleWhileDraggingPatch.cs` (`SnapshotFavourites`' old `!RemoteBackpackView.IsOpen` early return) + `reversing/…/PlayerCamera.cs:1904-1912` (`CloseContainer` on the shrunken ring) |

## 2. The change

- `PlayerCameraDragUsePatch`'s local-item branch: after the landed cross-player use-by-drag attempt,
  `ResolveDisplayProxyTarget` walks the same raycasts and reads the same things the native body reads — the
  first overlapping inventory button's `GetItem()`, the `ContainerBack` tag's `currentContainer`, and the
  clone-owned body slot (`isBody` while the ring renders the clone) — and when any of them names another player's
  displayed inventory the release is reported and cancelled before the native body can mutate anything. The
  classification stays the game's own: the seam resolves the target, it never decides which branch would run.
- New seam member `IRemoteBackpackPatchBridge.ReportLocalReleaseOntoProxy(Item dragItem, ulong
  targetOwnerSteamId, string target)`, implemented by `RemoteDragIntentDispatcher` as ONE Warning line naming the
  item, what the pointer was on and the owner that target belongs to — the owner is resolved by the caller and
  printed as resolved (the target item's own marker, or the displayed player when the marker carries none, which
  is the same fallback the release window opens its bracket with). No wire member, no intent, no protocol number.
- `PlayerCameraHandleWhileDraggingPatch.SnapshotFavourites` lost its view-open gate (row 9): the same field store
  on a proxy child is now put back and reported in the frame state the gate used to skip. A LOCAL item's flip is
  still left alone, so local play is unaffected.
- Deliberately NOT changed: the mutation seams, the intent vocabulary, the proxy-drag bracket, the world
  fallback, and the empty-slot case's *reporting* shape (it is refused like the rest, which is what the design
  record's §6 corner line now records).
- The defect ticket and `docs/backlog/README.md` are updated in this cycle's acceptance-record commit, together
  with the run's record — this commit is the change itself; the design record's §6 corner line, which describes
  this code, rides it.

## 3. Verification

| Mechanism × change × evidence | Evidence |
|---|---|
| The seam's census pins the new member — a member added to a seam is a red until it is reviewed here | `tests/CasualtiesUnknownOnline.NormativeGates.Tests/PatchBridgePortShapeGateTests.cs` `Seams` entry `IRemoteBackpackPatchBridge` + `ReportLocalReleaseOntoProxy`; the pin and the interface are compared as whole-string censuses, and the pre-fix interface declared no such member |
| The compiled port carries it (L0 reflection, the layer's own contract shape) | `tests/CasualtiesUnknownOnline.Tests/Patching/RemoteBackpackContractTests.cs` `PatchBridge_ExposesTheReleaseWindowSurface` — name, `void` return, `Item` + `ulong` + `string` parameters |
| The bridge implementation serves exactly its seams' members | `PatchBridgePortShapeGateTests.BridgeImplementation_ServesExactlyTheSeamMembers` (unchanged, green with the new member) |
| The runtime reading: the same gesture as batch `20261006-d` leaves the owner's item in its own bag, the proxy's contents unchanged, no `[ItemDropped]`/`[ItemDestroyed]`, and one refusal line — extended by the empty-slot half and held honest by a local control | READ by the three-client batch `20261006-e` on the deployed `0.1.0+6e07b388…`, record `docs/evidence/acceptance/local-item-into-remote-display-20261006-e.md`: E1 `castContentsAfter: 0`/`childInCast: false` with the owner's tree byte-identical before and after, E2 the same on an empty clone body slot, E3 the native move still running with no refusal line. The pre-fix red is batch `20261006-d`'s own record — `docs/evidence/acceptance/container-move-snapshot-only-sync-20261006-d.md` reads `castItemProxy: true`, `castItemOwner: 76561198863287957`, `childInCast: true` and the child's parent becoming `trashbag(Clone)`, and its Limits section carries that batch's artifact-delta statement against the earlier build |

## 4. Limits

- **No session ran while writing this change.** The Unity facts it rests on — that the ring's buttons resolve to
  the focused clone's proxies at release time, that the container window's panel is the proxy container while it
  is open, that the early-out leaves the view open — were read from `reversing/` and from batch `20261006-d`'s
  probe at the time, and batch `20261006-e` then settled the first two on the deployed artifact (the ring's proxy
  item and an empty clone body slot were both cast at, and both were refused with the proxy's contents unchanged).
  A green unit suite is still not that evidence, and the container-window branch was not cast at.
- **The refusal is per pointer target, not per native branch.** Any overlapping inventory button carrying a
  display-proxy item, the container window's own panel, and any body slot of the displayed clone cancel the
  release, whether or not the native body would have stopped there: a proxy item that is neither a container, a
  battery, nor a combine partner would have fallen through to the world fallback and dropped this client's item
  into the world, and a co-cast craft button, trader thing or wound-view limb would have been consumed by the
  native body first. That is deliberate — with another player's inventory under the pointer, "release my item
  here" has no local meaning, and a refusal is observable where the fallback drop is not what the player asked
  for. The radial centre is the one case argued disjoint from the ring — the ring's own `Overlaps` band is
  152-281 units from the radial menu (`reversing/…/InvButton.cs:31-39`) while the use circle reads 135
  (`.acceptance/20261005-d`, `.acceptance/20261006-a`) — but that is the source constant against a recorded
  radius, not a session that had both surfaces live at once (those probes ran with the ring closed), and it
  assumes the radial menu and the use circle share a centre.
- **The world fallback is argued, not proven closed.** Row 7 says why the guard does not read it and where that
  argument stops: a clone object's collider is disabled by the two paths that create one, so a prefab-leftover
  proxy kept by the reuse path could in principle still be reachable by `Physics2D.OverlapPoint` and receive a
  local item. No evidence establishes that such a proxy exists, and refuting it needs the prefab assets or a
  session; it is named here rather than smoothed over.
- **The favourite-gate change (row 9) is a code-reading fix, not a driven one.** This cycle's independent review
  found the gate; the state it guards (focus cleared while the container window is still up) was not entered in
  any session, so its reachability stays unproven. The change is strictly fail-closed: a proxy write that used to
  stand is put back, and a local item's write is untouched.
- **Not carried: a cross-player "give this item to that player" gesture.** Moving this client's own item into
  another player's inventory has no intent member and would be a new capability (wire, host validation, owner-side
  arbitration); this ticket refuses the gesture observably instead of adding it.
- **The cross-player use-by-drag attempt runs before the guard**, so a usable item released over the focused
  clone's ring is still consumed as the landed use request wherever `LocalUseItemEligibility` admits it. That path
  mutates no proxy; it is recorded so the guard's reach is not read as wider than it is.

Behavioural suite 4,637/4,637 and gate project 382/382 on the frozen tree, `dotnet format` clean.
