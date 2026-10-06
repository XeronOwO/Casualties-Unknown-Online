# A local item released onto a remote display proxy lands inside it

- Status: Done (batch `20261006-e`: the release is refused at the release seam before the native body runs — the
  owner's probe names the target as the other player's display proxy and reads its contents unchanged
  (`castContentsBefore: 0` → `castContentsAfter: 0`, `childInCast: false`), one `[RemoteIntent]` line names the
  item and the target, the owner's item tree is byte-identical to the pre-gesture one, both viewers stay quiet, the
  empty-slot half is refused the same way, and the local control still runs the native move. History: batch
  `20261006-d` found it while attributing batch `20261006-c`'s local control; fixed by `6e07b388` and read on
  `0.1.0+6e07b3882bdfa4733eff30b5dfd93d72daf041ca`. Record
  `docs/evidence/acceptance/local-item-into-remote-display-20261006-e.md`.)
- Priority: Medium
- Category: Item sync / call identity (a drag's TARGET, the mirror of the proxy-drag guard)
- Source: agent acceptance batch `20261006-d` (2026-10-06), found while attributing batch
  `20261006-c`'s local control; it violates the design rule the display proxy exists under —
  `RemoteBackpackView`'s own words, "remote clones are display proxies, so the native UI must
  never be allowed to mutate the focused clone".
- Related: `done/container-move-snapshot-only-sync.md` (the batch that found it),
  `review/remote-inventory-native-parity-rework`, `Patches/PlayerCameraDragUsePatch.cs`,
  `Patches/RemoteDragMutationPatches.cs`, `Patches/ContainerItemPatches.cs`,
  `Character/CloneInventoryRenderer.cs`
- Acceptance record: `docs/evidence/acceptance/local-item-into-remote-display-20261006-e.md`

## Symptom (evidence)

The owner drags one of its OWN carried bags while its inventory ring is focused on another player's
backpack, and releases it on a ring slot that shows that player's bag. The native container move ran and
the local item was loaded INTO the other player's display proxy: batch `20261006-d`'s
`m1-host-release-onto-proxy.json` reads the release's own target as `castItemProxy: true`,
`castItemOwner: 76561198863287957`, `castIsContainer: true`, `castContentsBefore: 0` →
`castContentsAfter: 1`, `childInCast: true`, the child's parent becoming `trashbag(Clone)`, at a `toCast`
distance of 0.178 units.

The owner's own log then shows the move as a departure nothing consumes and, ~1.4 s later, a real drop:

```text
12:46:27.499 [ItemTrace] op=7 begin item=1104127576003 origin=OnItemUnloadedFromContainer event=Unload
12:46:27.501 [ContainerUnload] dogfood (id 1104127576003) left its container into the world from the
           CarriedInventory side — …
12:46:28.942 [ItemDropped] dogfood (id 1104127576003) at (1.0,445.4), vel (0.0,0.0) — container contents 0.
12:46:28.953 [ItemTrace] op=8 item=1104127576003 origin=OnItemDestroyed result=Committed(1) events=[Destroyed]
```

The player-visible outcome is item loss: the child leaves its bag, sits inside the proxy for a moment, is
dumped into the world by the clone render rebuild (`CloneInventoryRenderer` unloads a proxy container's
children while it rebuilds), and is destroyed 11 ms after the drop report — the position
`(1.0,445.4)` is the unload position plus `Container.UnloadItem`'s 1.5-unit lift, i.e. the item never moved
anywhere the player meant it to.

## What the attribution found (2026-10-06)

- **The release window only covered a proxy DRAGGED item.** `PlayerCameraDragUsePatch.Prefix` returned early
  for a local dragged item (`dragItem.GetComponent<RemoteCloneRender>() == null`) and opened
  `RemoteDragIntentWindow` only for a proxy, so the native body executed the container branch unguarded on
  the TARGET side while the dragged item was the client's own.
- **Nothing reported it, by design.** Both container hooks skip a display-proxy container
  (`ContainerItemPatches`: `!RemoteCloneContainerGuard.IsDisplayProxy(__instance)`), so the load produced no
  carrier at all; the departure was left to the frame-end settle, and the clone rebuild is what made the
  item a world item — the drop the log above records is CUO reporting what really happened, not a defect in
  the reporting.
- **It is the same call-identity family as the proxy-drag guard**, one side over: `RemoteDragMutationPatches`
  states "a display proxy is never mutated" and enforces it for the dragged item; the TARGET of a local drag
  had no such enforcement.
- **Batch `20261006-c`'s "unexplained native refusal" is this same gesture**, not a refusal: that batch's
  local control cast at a button its own list resolves to the guest's bag proxy, and batch `20261006-d`
  reproduced the shape by intent — with the load landing in the proxy rather than being refused.

## What landed (commit `6e07b388`, read by batch `20261006-e`)

- **The release seam resolves the target the way the native release body itself does** and cancels the
  release before the native body runs when that target belongs to another player's displayed inventory: the
  overlapping inventory button's `GetItem()` (the native gate and the value `TryPerformInventoryAction`
  itself reads), the container window's `ContainerBack` panel, and a body slot of the displayed clone —
  whose index the native R8/R9 sequence would otherwise run against the LOCAL body.
- **Cancelling clears the drag**, so the native body takes its own "no drag item" early-out and no branch
  runs at all. That is why the fix is at the release seam and not at the mutation seams: R5's expansion
  unloads each child out of the dragged item's OWN container first, and that half carries no proxy operand —
  refusing only the load would have dumped the children into the world instead.
- **One new seam member**, `IRemoteBackpackPatchBridge.ReportLocalReleaseOntoProxy(Item, ulong, string)`,
  prints one Warning line naming the dragged item, the target and the owner that target belongs to. No wire
  member, no intent, no protocol number.
- **Not routed as an intent, deliberately.** Moving this client's own item into another player's inventory
  has no member in the frozen intent vocabulary and would be a new capability (wire shape, host validation,
  owner-side arbitration). The family's rule — the viewer's backpack renders the owner's facts and its
  gestures produce intents — leaves an unrepresentable gesture refused and observable; a cross-player "give
  this item to that player" gesture, if ever wanted, is its own ticket.
- **The while-dragging favourite snapshot lost its view-open gate** (this cycle's independent review found
  it): the focus can already be cleared while the game still shows the container window it opened, and in
  that frame state a proxy child's `favourited` write was left standing. A local item's flip is untouched.

## Readings (batch `20261006-e`, 2026-10-06)

| Row | What was driven | Verdict | Evidence |
|---|---|---|---|
| E1 | The owner releases its own bag onto the ring slot showing the other player's bag proxy, expansion key held | pass | `m1-host-release-onto-proxy.json` (`castItemProxy: true`, `castContentsAfter: 0`, `childInCast: false`), one refusal line in `e1-host-window.txt`, owner tree unchanged (`m7` = `m5` by SHA-256), viewers quiet |
| E2 | The same owner releases its own item onto an EMPTY body slot of the displayed clone | pass | `m2-host-release-onto-empty-body-slot.json` (`castIsBody: true`, `castSlot: 1`, `castItem: "none"`), the second refusal wording, owner tree unchanged (`m9` = `m5`), `Drop=0; Destroy=0` in that window |
| E3 | The local control: the same owner releases onto its OWN bag with no remote view | pass | `m6-host-local-control.json` (`castItemProxy: false`, `castContentsAfter: 2`, `childInCast: true`), the native pair and no refusal line in `e3-host-window.txt` |

## Non-goals

- Not the container-move carrier: that is fixed and `done/container-move-snapshot-only-sync.md`'s rows pass.
- Not the divergence monitor: it stayed silent here because nothing compared a proxy's contents, which is a
  separate question from this defect.
- Not the native `Container.LoadItem` guards: no CUO-side guard can make a display proxy a legitimate target.
- Not a cross-player item hand-over: this ticket refuses the gesture observably; the capability would need
  its own intent member and design.
- Not the world fallback: the guard does not read it, and the reason is narrower than "proxies have no
  colliders" — the paths that create a clone object disable its collider, while the reuse path keeps an
  existing child; a prefab-leftover proxy is named as a residual in the cycle's self-check rather than
  smoothed over.
