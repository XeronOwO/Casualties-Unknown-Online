# Acceptance record — Container moves reach the viewer as a snapshot, not an event

- Ticket: `container-move-snapshot-only-sync` — verdict: **every row passes**, the ticket moves to `done/`
  (this batch re-read row A1g′ and row A1z, the two rows batch `20261006-c` left open, and attributes the
  native refusal that batch could not stage; row A1g stands from `20261006-c` against an artifact whose
  only source delta is a comment line)
- Batch: `20261006-d` — ticket `container-move-snapshot-only-sync`
- Commit: `a10abcf1` · Deployed artifact: `CasualtiesUnknownOnline.dll`,
  ProductVersion `0.1.0+a10abcf1ac3f4bc477c4aa96883ddecf8a696f29`
- Run: 2026-10-06 12:41 → 12:52 local · Host: physical machine · Guest + third peer: Sandboxie sandboxes
- Dependencies: `game`, `deploy`, `steam`, `sandboxie`, `sandbox-alt`, `hotrepl`, `capture`, `input`, `logs`,
  `artifacts` (preflight: 11 present, `RESULT: OK`)
- Artifacts: the JSON probes and the byte-marked log windows named below, in the directory
  `acceptance-artifacts-dir` under `20261006-d/`

## The rows as planned before the run

| # | Row | Class | Verdict | Evidence |
|---|---|---|---|---|
| A1g′ | The container expansion driven LOCALLY: the owner's own release, the expansion key held, no operator — the owner's child load announces the TARGET container's fact and no viewer warns | machine | **pass** | `m6-host-local-control.json` (the target named: the client's own bag, not a proxy), the owner's window in `marks-a3.txt`, the owner's tree `m7-host-tree-after.json` |
| A1z | Zero warnings on both viewers across the gesture window plus one quiet cycle | machine | **pass** | `marks-a3.txt` on `guest` and `alt` (no `divergence`, no warning at all), and the same window on `host` |
| A1g | The same gesture driven REMOTELY, both owner directions | machine | **stands from `20261006-c`** | not re-driven here; the artifact delta is a comment line (below) |
| A1d | Container insert, take-out, slot release and the drop kinds, both owner directions | machine | **stands from `20261006-a`** | not re-driven by `20261006-c` or by this batch |

## Row A1g′: what the run read

The owner is the host; no operator is involved. The fixture (identical in shape to `20261006-c`'s) is two
`trashbag`s it carries itself — bag A `1095537641411` in slot 0 holding two `dogfood`s, bag B
`1099832608707` in slot 1 empty (`m5-host-tree.json`) — created through `item-provide mode=create` and
filled through `container-fill`.

The local release needs the game's own ring OPEN, because the ring's `InvButton`s do not exist in the scene
while it is closed: a list taken with the ring shut reads `buttonCount: 0`. The ring is opened the way a
player opens it — the game's own `toggleinventory` key (`Tab`) queued on the client's own window, with no
drag staged, so the per-frame guard that force-closes the ring while an item is dragged
(`PlayerCamera.cs:1778`) does not apply. The key the gesture itself needs (`expanddesc`, `LeftShift`) is
held and read back before the release: `heldAtEnd: true`, `osKeyAtEnd: 0`, `isForeground: false`
(`k5-host-held.json`) — the hold never leaves the process.

The release names its own target (`m6-host-local-control.json`): the cast resolves to a ring `InvButton`
(`castKind: button`, `castOverlaps: true`, slot 1) whose item is a `trashbag` with
`castItemProxy: false`, `castItemOwner: 0`, `castItemParent: InvSlot (4)` — the client's OWN bag B — and
both of the dragged bag's children read `canHold: true` at a `toCast` distance of 1.852 units, with
`castContentsBefore: 0` → `castContentsAfter: 2` and `childInCast: true`. The native effect is in the
owner's own tree: bag A is empty afterwards and bag B holds both `dogfood`s (`m7-host-tree-after.json`).

The owner's own client reports the move as the fixed pair does, once per child — the departure is
registered, consumed by the load that completes it, and the ONE report is the TARGET root's contents fact:

```text
12:49:29.061 [ItemTrace] op=15 begin item=1108422543299 origin=OnItemUnloadedFromContainer event=Unload
12:49:29.061 [ContainerUnload] dogfood (id 1108422543299) left its container into the world from the
           CarriedInventory side — …
12:49:29.061 [ItemTrace] op=15 item=1108422543299 origin=OnItemLoadedIntoContainer result=Cancelled
           events=[LoadedIntoContainer]
12:49:29.062 [ItemTrace] op=16 item=1108422543299 origin=OnItemLoadedIntoContainer result=Committed
           events=[ContainerContent]
12:49:29.062 [ContainerLoad] dogfood (id 1108422543299) moved inside body container trashbag — root content
           event up to trashbag (id 1099832608707).
```

The second child reads identically (`op=17`/`op=18`, `1112717510595`), so the multi-child expansion is two
complete target-root captures and no pickup or world-drop report. Both viewers applied exactly that fact —
`[CarriedSync] applied trashbag (id 1099832608707) to 76561198281246659's snapshot — re-rendering the
clone.` once per child on `guest` (12:49:29.085/.086) and on `alt` (12:49:29.078/.079) — and **neither
client logged a divergence or any warning in the window**: `marks-a3.txt` filtered for `divergence|WRN|ERR`
reads `NO MATCHING LINES` on both viewers and on the owner, over the gesture plus a quiet cycle longer than
one 1 Hz character snapshot. Row A1z passes in the same window the row A1g′ gesture produced.

## The `20261006-c` anomaly, attributed

Batch `20261006-c` drove a "local control" four times and could not stage it: the same log shape appeared
each time (the child's departure registered, no load report, the departure settling as a real drop 1.2–1.8 s
later and the item destroyed 11 ms after that), and that batch recorded the native refusal as unexplained
and asked for a position probe on a fresh session. This batch carries the probe and the attribution, and it
is a staging artifact rather than a refusal — with the evidence sitting in that batch's own artifact set:

1. **The button those attempts cast at was never the client's own bag.** `20261006-c`'s own list, taken with
   the host's ring focused on the guest's backpack (`g6-host-list.json`), resolves its `cast=4` to slot 1 —
   and that button's item is `itemId: 13787924117, itemOwner: 76561198863287957, proxy: true`, i.e. the
   GUEST's bag rendered as a display proxy. No artifact closes that focus before the four attempts, and the
   only `[BackpackView]` line in the host's log is the open (`opened native backpack view for
   76561198863287957 (acceptance).`, 12:16:32.715). The ring focus redirects `InvButton.GetItem()`
   (`InvButtonBodyPatch`), so the release aimed at a remote-displayed container while the dragged item was
   the client's own bag — a mixed gesture, not the local control the row needs.
2. **That gesture produces exactly the log shape read as a refusal.** This batch reproduced the shape by
   intent (a local bag released onto the ring button that shows the guest's bag): the load LANDS in the
   display proxy — `m1-host-release-onto-proxy.json` reads `castItemProxy: true`,
   `castItemOwner: 76561198863287957`, `childInCast: true`, the child's parent becoming `trashbag(Clone)` —
   and the owner's log still shows no load report and no consume, because both container hooks skip display
   proxies (`ContainerItemPatches.cs`), then the departure settles ~1.4 s later as a real drop at the
   unload position and the item is destroyed. So "the child stayed at the unload position and settled as a
   drop" does NOT discriminate a refused load from a load into a proxy: in `20261006-c` the clone render
   rebuild is what put the child back into the world.
3. **The two guards that batch suspected are excluded by measurement.** A `trashbag` accepts a second
   `dogfood` (`c4-host-fill-two.json`: `canHold: true`, `loaded: true`, `contentsAfter: 2`), so a target
   that already holds one item is not refused by weight; and with the ring focused on the guest this
   session's proxy target stood 0.178 units from the dragged bag (`m1-host-release-onto-proxy.json`), so
   the ten-unit distance guard was nowhere near refusing. `Container.LoadItem`'s silent paths are the two
   alerts (`alertcontainerstacking`, `alertcontainerstackingalt`) and the final guard; the alerts did not
   fire in `20261006-c` (the item is not a container, and a carried bag's parent is a slot, not a
   container — `container-fill` loads into the same shape without an alert).

What `20261006-c` could not know, and what this batch's own reading shows, is the staging rule behind it:
the ring's buttons exist only while the ring is open, and a run that needs a LOCAL target must drive the
gesture with the ring focused on the client's OWN body. The release result now reports the target it
resolved — identity, display proxy, owner, position, and the guard's inputs on each child — so a release
can never again be read without knowing what it aimed at.

## A sibling defect this run found (ticketed, not fixed here)

A local dragged item released onto a ring button that shows a REMOTE player's container is loaded into that
display proxy: the drag window covers a proxy DRAGGED item, not a proxy TARGET, so the native move runs
(`m1-host-release-onto-proxy.json`), nothing reports it (both hooks skip display proxies), and the clone
render rebuild later unloads the foreign child into the world — the owner's item leaves its bag and lands on
the ground. Filed as `backlog/done/local-item-into-remote-display.md` with this evidence;
batch runs do not change code, so it is not fixed here.

## Limits

- **One session, one reading per row.** Row A1g′ was driven once, with two children in one expansion (a
  two-report case); row A1z's zero covers that window plus a quiet cycle. Consistent, not a claim about
  rarity.
- **Row A1g is not re-driven here.** It passed in batch `20261006-c` against `0.1.0+8da00be3…`; the artifact
  this batch deployed differs from that one by ONE comment line
  (`ContainerLoadClassifier.cs`, the ticket's own path in its doc comment, `git show 7e1d6da8`) plus this
  batch's acceptance-recipe probe, which is not part of the plugin. The shipped behaviour is unchanged, and
  the row's verdict stands as that batch recorded it rather than being re-claimed.
- **Row A1d is not re-driven** by this batch either; its `20261006-a` verdicts stand.
- **The cast index is scene-order dependent**, and the record keeps the indices it used: the ring's slot-1
  button was index 4 with a bare ring, index 6 with the container panel open (its two `ContextMenu` buttons
  come first), and unresolvable at all with the ring closed (`buttonCount: 0`). Every release in this batch
  names its resolved target, so the index is evidence and not an assumption.
- **The fixture is the batch's own staging** (`item-provide mode=create` + `container-fill`), as in batches
  `20261006-b` and `20261006-c`; the gesture itself is the game's own release path, with the game's own
  `expanddesc` and `toggleinventory` binds held through queued window messages.
- **The owner's body was not moved or repaired for this batch** (the ring opens on its own after the key
  toggle, so no consciousness override was needed).
