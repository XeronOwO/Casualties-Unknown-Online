# A local item released onto a remote display proxy lands inside it

- Status: Todo
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

## Symptom (evidence)

The owner drags one of its OWN carried bags while its inventory ring is focused on another player's
backpack, and releases it on a ring slot that shows that player's bag. The native container move runs and
the local item is loaded INTO the other player's display proxy: batch `20261006-d`'s
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

- **The release window only covers a proxy DRAGGED item.** `PlayerCameraDragUsePatch.Prefix` returns early
  for a local dragged item (`dragItem.GetComponent<RemoteCloneRender>() == null`) and opens
  `RemoteDragIntentWindow` only for a proxy, so the native body executed the container branch unguarded on
  the TARGET side while the dragged item was the client's own.
- **Nothing reports it, by design.** Both container hooks skip a display-proxy container
  (`ContainerItemPatches`: `!RemoteCloneContainerGuard.IsDisplayProxy(__instance)`), so the load produced no
  carrier at all; the departure was left to the frame-end settle, and the clone rebuild is what made the
  item a world item — the drop the log above records is CUO reporting what really happened, not a defect in
  the reporting.
- **It is the same call-identity family as the proxy-drag guard**, one side over: `RemoteDragMutationPatches`
  states "a display proxy is never mutated" and enforces it for the dragged item; the TARGET of a local drag
  has no such enforcement.
- **Batch `20261006-c`'s "unexplained native refusal" is this same gesture**, not a refusal: that batch's
  local control cast at a button its own list resolves to the guest's bag proxy, and this batch reproduced
  the shape by intent — with the load landing in the proxy rather than being refused. The container ticket
  is closed on that reading; this ticket is the defect the reading exposed.

## What it needs

A local dragged item whose resolved release target is a display-proxy container must not reach the native
move: fail closed with the same one-line report the proxy drag has
(`ReportRemoteDragUnresolved`), or route it as an intent for the proxy's owner to run on its real item —
whichever the family's design settles on, decided at the release seam rather than inside the native body.
The record of the run that found it is
`docs/evidence/acceptance/container-move-snapshot-only-sync-20261006-d.md`.

## Non-goals

- Not the container-move carrier: that is fixed and `done/container-move-snapshot-only-sync.md`'s rows pass.
- Not the divergence monitor: it stayed silent here because nothing compared a proxy's contents, which is a
  separate question from this defect.
- Not the native `Container.LoadItem` guards: no CUO-side guard can make a display proxy a legitimate target.
