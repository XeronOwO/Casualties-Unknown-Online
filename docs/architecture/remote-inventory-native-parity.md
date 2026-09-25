# Remote Inventory Operations: Native Parity Design

Status: **stage 0 design record** — decisions only, no behaviour change. Stages 1-4 of
`docs/backlog/todo/remote-inventory-native-parity-rework.md` are not implemented yet, so the
clone-edit path this document replaces is still the live code.

The `reversing/Assembly-CSharp/Assembly-CSharp/*.cs` anchors below carry line numbers on purpose:
that tree is never edited (it is the decompiled game and is not tracked), so its line numbers are
stable, while our own sources are cited by path plus quoted text.

## 1. Why the current shape cannot reach parity

One remote operation is executed in two worlds that meet only at the 1 Hz character snapshot and the
item event stream:

| Half | Where it lives | What it does |
|---|---|---|
| Gesture classification | requester's client, `GameAdapter/Patches/PlayerCameraDragUsePatch.cs` | re-derives the gesture from a hand-written ordering (UI-only window, container move, radial centre, inventory button, pour, edge drop, take) into a CUO operation enum |
| Mirror edit | host, `Runtime/Session/PlayerInteraction/PlayerRemoteInventoryService.cs` | clones the owner's `CharacterDataMsg`, edits the item tree inside the clone, issues a kernel item command built from the clone, saves the clone back as authoritative |
| Owner-side apply | owner's client, `GameAdapter/RemoteInventoryOperationApply.cs` | runs real native calls, but only for the kinds that reach it (`Combine`, `Use`, `Wear`, `BatteryLoad`, `BatteryUnload`, `FavoriteToggle`, `MoveToSlot`) |

Two structural consequences, both reported by the user:

- The mirror edit loses to the next authoritative report from the real body — that is the reported
  "taken out of the trash bag, then it disappears a short while later".
- A native gesture the ordering does not name does nothing and reports nothing — that is the
  reported "nothing inside can be operated at all". The ordering is also wrong about the native
  gesture family in at least three places (§2.2): the native drain and the native favourite toggle
  are *while-dragging* actions, not release branches, and the native battery branch guards on the
  target carrying a battery component while the DIRECTION comes from the dragged item's own tags
  (`tool` unloads, `battery` loads) — there is no direction argument to classify.

Every native rule the operation should inherit (stacking, container capacity and tag guards, slot
rules, item animations, item sounds) is therefore either re-implemented on the host or missing.

## 2. The native gesture pipeline (mechanism inventory)

Entry: `PlayerCamera.Update` calls `HandleDragging(...)` (`PlayerCamera.cs:1338`), which drives three
phases off the `iteminteract` bind (`PlayerCamera.cs:1353`):

```text
GetKeyDown(iteminteract) -> HandleStartDragging      (PlayerCamera.cs:1357)
GetKeyUp(iteminteract)   -> HandleReleaseDragging    (PlayerCamera.cs:1361)
every frame              -> HandleWhileDragging      (PlayerCamera.cs:1363)
```

### 2.1 Drag start — `HandleStartDragging` (`PlayerCamera.cs:1367`)

| Gate / step | Native call | Anchor |
|---|---|---|
| Not conscious, or `allowUseItem` is false | returns (the second with `UseFailUnhappiness`) | `PlayerCamera.cs:1369`, `:1375` |
| Hover an inventory label | `dragItem = itemLabel.refItem` | `PlayerCamera.cs:1405` |
| Hover an inventory button that passes its own overlap filter | `dragItem = invButton.GetItem()` | `PlayerCamera.cs:1411` |
| Hover a world item | `dragItem = collider2D.GetComponent<Item>()` | `PlayerCamera.cs:1437` |
| Hover a player collider | opens the radial instead of dragging | `PlayerCamera.cs:1442` |
| Hover a usable object in range | `usableObject.SendMessage("OnUse")` | `PlayerCamera.cs:1449` |

The start phase decides only *what is being dragged*. It never mutates inventory.

### 2.2 While dragging — `HandleWhileDragging` (`PlayerCamera.cs:1713`)

Two of the operations the current CUO design treats as release branches are native *continuous*
actions:

| Gesture | Native call | Anchor |
|---|---|---|
| Drag a water container over the drain object while the target block has no liquid and `body.DoPickupCheck(item, true)` passes | `WaterContainerItem.Drain(CalculateDrain(0.2f * Time.deltaTime * Capacity))` — every frame, not once | `PlayerCamera.cs:1729` |
| Hold the favourite bind while hovering an inventory button | `invButton.GetItem().favourited = !...` — the *hovered* item, not the dragged one | `PlayerCamera.cs:1745` |

Consequence for the design: a "pour" and a "favourite" intent are produced by these while-dragging
sites, and a design that only inspects the release loses both.

### 2.3 Release — `HandleReleaseDragging` (`PlayerCamera.cs:1456`)

The release is a **hit-test dispatch**, not a classification table: it walks the UI raycast results
in order and stops at the first hit that answers "consumed".

```text
DoPickupCheck(dragItem, false)                                   (PlayerCamera.cs:1467)
  false -> drop the drag, return
TryPerformUIActions(uiCasts)                                     (PlayerCamera.cs:1500)
  for each UI cast, in order:
    TryPerformInventoryAction(hit, uiCasts)                      (PlayerCamera.cs:1529)
    TryPerformRadialAction(hit)                                  (PlayerCamera.cs:1636)
    TryPerformSpecialUIAction(hit)                               (PlayerCamera.cs:1654)
  no hit consumed -> TryPerformWorldActions()                    (PlayerCamera.cs:1686)
```

Every branch and the exact native call it makes:

| # | Native condition | Native call(s) | Anchor |
|---|---|---|---|
| R1 | hit is an `InvButton` (passing `Overlaps`) whose item is the dragged item | nothing — the release is consumed | `PlayerCamera.cs:1536` |
| R2 | hit item has a `battery`, dragged item has tag `tool` | `hitItem.battery.UnloadBattery(false)` | `PlayerCamera.cs:1543` |
| R3 | hit item has a `battery`, dragged item has tag `battery` | `hitItem.battery.LoadBattery(dragItem)` | `PlayerCamera.cs:1548` |
| R4 | hit item is a `Container` (plain release) | `container.UnloadItem(dragItem, null)` then `container.LoadItem(dragItem)` | `PlayerCamera.cs:1567` |
| R5 | hit item is a `Container`, `expanddesc` held, hit item is not the dragged item | for each direct child that `CanHoldItem`: `dragItem.container.UnloadItem(child, null)` + `container.LoadItem(child)` | `PlayerCamera.cs:1585` |
| R6 | `body.CanCombine(hitItem, dragItem)` | `body.CombineItems(hitItem, dragItem)` | `PlayerCamera.cs:1600` |
| R7 | hit is a body slot, dragged item is wearable and not `wearableCanBeHeld` | native alert only, consumed | `PlayerCamera.cs:1609` |
| R8 | hit is a body slot, slot and drag target are both held items | `body.SwapSlots(hitSlot, body.SlotOf(dragItem))` | `PlayerCamera.cs:1614` |
| R9 | hit is a body slot, otherwise | `body.DropItem(dragItem)` if held, `body.DropItem(hitSlot)` if occupied, then `body.PickUpItem(dragItem, hitSlot, false)` | `PlayerCamera.cs:1621` |
| R10 | hit is tagged `RadialCenter`, the radial is open and the pointer is inside the circle | `body.WearWearable(dragItem)` if wearable; `body.UseItem(dragItem)` if usable; consumed either way | `PlayerCamera.cs:1640` |
| R11 | hit carries a `WoundViewLimb` | `ApplyWoundItem(dragItem)` → `Stats.useLimbAction(limb, item)` or `WaterContainerItem.ApplyToLimb(limb, 100f)` | `PlayerCamera.cs:1658`, `:739`, `:750` |
| R12 | hit is tagged `TradeThing` and a trader is open | `currentTrader.GiveItem(dragItem)` | `PlayerCamera.cs:1661` |
| R13 | hit is tagged `ContainerBack` and a container window is open | `currentContainer.UnloadItem(dragItem, null)` + `LoadItem(dragItem)` | `PlayerCamera.cs:1666` |
| R14 | hit is the craft button | `OpenCraftScreen()` + `SeeRecipesWithItem(dragItem)` | `PlayerCamera.cs:1673` |
| W1 | no UI hit consumed, the dragged item's parent is a `Container` | `container.UnloadItem(dragItem, null)` | `PlayerCamera.cs:1689` |
| W2 | no UI hit consumed, the item is held, the wound view is closed, the pointer moved > 10 px | `body.DropItem(dragItem)` | `PlayerCamera.cs:1694` |
| W3 | same, item is worn and worn by the body | `body.DropWearable(dragItem)` | `PlayerCamera.cs:1698` |
| W4 | same, a world `Container` sits under the pointer and the dragged item is not a container | `container2.UnloadItem(dragItem, null)` + `container2.LoadItem(dragItem)` | `PlayerCamera.cs:1704` |

Two post-steps of the release are presentation state, not mutations: a container dragged without
moving the pointer opens the container window (`PlayerCamera.cs:1477`), and the open container window
and the unwear buttons are repopulated (`PlayerCamera.cs:1490`, `:1494`).

### 2.4 The mutation-side guards (why the owner must execute)

The dispatch calls carry no authority of their own — the guards live inside the callee and read the
*local* scene:

| Guard | Rule | Anchor |
|---|---|---|
| `Container.CanHoldItem` | per-item weight, total weight, and the container's tag restriction | `Container.cs:110` |
| `Container.LoadItem` | refuses a nested container that still holds something, refuses a container inside a container, refuses itself, then requires `CanHoldItem` **and** a distance below 10 world units | `Container.cs:116` |
| `Container.UnloadItem` | detaches the child, re-enables its rigidbody, lifts it 1.5 units | `Container.cs:154` |
| `Body.DoPickupCheck` | held, or held/worn parent container, otherwise a ground linecast plus a distance below 10 units | `Body.cs:1356` |
| `Body.PickUpItem` | slot may be picked up (or `force`), item not already held, slot free, `DoPickupCheck` (or `force`), `onlyHoldInHands` restricts to hand slots; unloads the parent container itself and plays the backpack sound | `Body.cs:1388` |
| `Body.SwapSlots` | drops both slots, then picks both up with `force` | `Body.cs:1413` |

Only the owner's own client has the scene these guards read, and the requester's drag geometry is
not the owner's. So the *call* travels, and the guard is evaluated where the objects are.

## 3. The design

### 3.1 Roles

| Role | Decides | Never does |
|---|---|---|
| Requester's client (the viewer) | which native gesture happened, what the intent is, and whether the target is visible and reachable *from its own view* | mutate a display proxy; predict the owner's item state; ask the host to judge its own sight |
| Host | what is genuinely its own: the session permission policy, membership, the ownership fact, first-writer-wins arbitration between competing intents, and the commit/broadcast of the resulting fact | model inventory contents; edit a clone of the owner's character data; judge the actor's reach or the owner's body from stale streamed positions |
| Owner's client | the operation itself: the native call on the real objects, with the real guards, animations and sounds; and the authoritative report of the result | execute a peer's intent that the session did not admit |

Who judges what follows the landed rule for this family (`docs/backlog/review/remote-interaction-local-gating.md`): the
actor's client judges reach and sight, the target's client judges what its own body and inventory
allow, and the host keeps arbitration and the commit of the effect. Decision 154's explicit
cross-player authority policies still decide *whether* this family of operations is enabled at all.

### 3.2 The capture seam

The viewer keeps running the game's own gesture code path; CUO intercepts only the *mutation calls*
that path makes:

1. `HandleReleaseDragging` opens a **release window** when the dragged item is a remote display
   proxy (and a second window kind for the two while-dragging sites of §2.2). The window is closed by
   the same patch's finalizer, so a throwing native body cannot leak it into the next frame. It is a
   **call bracket, not a time bracket**: it spans exactly one `HandleReleaseDragging` invocation (or
   one `HandleWhileDragging` frame), so the only calls inside it are the ones the native body itself
   makes. A projection rebuild — `CloneInventoryRenderer` calls `Container.LoadItem` and
   `Container.UnloadItem` on its clones while it rebuilds the remote backpack — a packet pump, or any
   other Unity callback cannot be captured by construction, and the interception stays a local drag
   concern rather than a global one.
2. Inside the window, the native mutation entry points of §2.3 are intercepted at the call the
   dispatch itself makes — `Container.LoadItem` / `Container.UnloadItem`, `Body.PickUpItem` /
   `Body.SwapSlots` / `Body.DropItem` / `Body.DropWearable` / `Body.UseItem` / `Body.WearWearable` /
   `Body.CombineItems`, `BatteryItem.LoadBattery` / `BatteryItem.UnloadBattery`,
   `WaterContainerItem.Drain`, the `favourited` write, `ApplyWoundItem`, `Trader.GiveItem`. The call
   is recorded as an intent and its original is skipped; its internals never run on a proxy.
   Intercepting at this level — not at the dispatch — is what makes the classification the game's
   own: the ordering in §2.3, its predicates and its early exits are all executed by the game, so
   CUO owns no ordering table at all.
3. Because the rest of the native branch still runs, the requester keeps the native *local* feedback
   of the gesture (`PlayBackpackSound`, the slot scale changes, `DoAlert` alerts, the radial and
   container-window state). Nothing about that feedback is re-implemented.
4. One order is coalesced, and only that one: `Container.UnloadItem(item, null)` immediately followed
   by `Container.LoadItem(item)` **on the same container** is one move into that container (R4, R5,
   R13, W4), while an `UnloadItem` with no matching load on its own container is a take into the
   world (W1). The window buffers its calls and emits the intents when it closes, so the pair becomes
   one intent instead of two. The merge is per container on purpose: `TryPerformWorldActions` runs its
   four steps as independent `if`s, so a take-out of one container followed by a move into a
   DIFFERENT one is the native order, and two intents reproduce it. Merging those into one would make
   the take-out conditional on the second container accepting the item, which the native code never
   does — `Container.LoadItem`'s refusal leaves the item exactly where the take-out put it.
5. The window fails closed and loud: a release that the native dispatch consumed but that produced no
   intent is logged as an unclassified gesture. A native gesture CUO has never seen must be
   observable, never a silent no-op.

### 3.3 The intent vocabulary (wire)

The wire carries the native call identity plus its operands, keyed by authoritative instance ids —
never a CUO semantic taxonomy, and never a mirrored result.

| Intent | Native call it replays | Operands |
|---|---|---|
| `MoveIntoContainer` | `container.UnloadItem(item, null)` + `container.LoadItem(item)` (R4, R13, W4) | item, container |
| `TakeOutOfContainer` | `container.UnloadItem(item, null)` (W1) | item |
| `MoveContainerChildren` | R5's per-child loop, evaluated by the owner | dragged item (the native source is `dragItem.container` — the dragged item's OWN container component, so the item is the operand and its children are resolved on the owner), target container |
| `PickUpToSlot` | `body.PickUpItem(item, slot, false)` (R9 first half) | item, slot |
| `SwapSlots` | `body.SwapSlots(slot, body.SlotOf(item))` (R8) | item, slot |
| `DropItem` | `body.DropItem(item)` (R9 second half, W2) | item |
| `DropWearable` | `body.DropWearable(item)` (W3) | item |
| `UseItem` | `body.UseItem(item)` (R10) | item |
| `WearItem` | `body.WearWearable(item)` (R10) | item |
| `CombineItems` | `body.CombineItems(target, item)` (R6) | item, target item |
| `LoadBattery` | `target.battery.LoadBattery(item)` (R3) | item, target item |
| `UnloadBattery` | `target.battery.UnloadBattery(false)` (R2) | target item |
| `ToggleFavourite` | the `favourited` write (§2.2) | item |
| `Drain` | `WaterContainerItem.Drain(amount)` (§2.2) | item, amount |
| `ApplyToLimb` | `ApplyWoundItem` → `useLimbAction` / `ApplyToLimb` (R11) | item, limb |
| `GiveToTrader` | `currentTrader.GiveItem(item)` (R12) | item |
| `TransferToBody` | the two-sided transfer: the owner releases (`Container.UnloadItem` / `Body.DropItem`), the destination body runs R9's own sequence — drop the occupying slot item when the destination slot is held, then `Body.PickUpItem(item, slot, false)` | item, destination body, slot |

`TransferToBody` is the one intent with no single native call behind it, because the native world has
no cross-player transfer: its two halves are the native release on the owner's side and the native
pickup on the destination side, and the host arbitrates the item id first-writer-wins between them.
R1 and R7 produce no intent (they are consumed native no-ops), and R14 plus the container-window
open of §2.3 are local UI on the viewer and never travel.

A slot intent's **body operand comes from the hit itself**, not from a CUO rule. An inventory button
of the owner's *projected* body resolves to the owner — the item stays the owner's and changes slot
on the owner's body — while a button of the requester's *own* body resolves to the requester, which
is `TransferToBody`: the gesture reported as the Tab transfer, with the requester's slot as the
destination. The native `Body.PickUpItem(item, slot, false)` the local branch made is then executed
by whichever body the intent names — carrying R9's occupying-slot step in front of it, because
`Body.PickUpItem` refuses a slot that is already held and the gesture that matters most here (the
transfer onto the requester's own inventory) is normally made while the requester is holding
something. The destination half is therefore R9's sequence, not a bare pickup. No intent can name a
body the requester may not act on without the host seeing it: the host is the only role that holds
both peer identities and the item's ownership fact, so an arriving intent whose destination body is
neither the owner nor the requester is refused there, with one logged line naming the intent, the
item and the destination. That is how cross-player remote-to-remote handoff stays out of scope
without becoming a silent no-op.

The owner executes an arriving intent by calling the same native method on the objects its own scene
resolves from the operands, and reports the result through the existing authoritative item/character
paths. A guard refusal (container full, blocked slot, `onlyHoldInHands`) is the native refusal: the
owner reports it, the host records it, the requester's projection stays on the owner's facts, and one
observable line names the intent, the item and the native reason.

### 3.4 Display stays a projection

The viewer's backpack keeps rendering the owner's facts through the existing projection seams —
`RemoteItemPresentation.SourceValues` for item source values and `CloneInventoryRenderer` for the
clone tree. This design changes *interaction* only: the viewer still never mutates a proxy, and the
projection still rebuilds from authoritative facts. The two projection tickets adjudicated in §5 are
therefore the display half of this feature, not a competing design.

Feedback splits the same way and needs no new channel: the *branch-level* feedback runs on the
requester's client because the game's own branch runs there (`PlayBackpackSound`, alerts, the slot
scale changes, the radial and container-window state), while feedback produced *inside* a mutation
call — an item's own animation and sound — happens where the mutation happens, on the owner's client,
and reaches the requester through the existing item and sound sync paths. Stage 3 audits which item
sounds those paths already carry before any of them is re-sent; the design adds no second feedback
path.

### 3.5 Protocol

The intent vocabulary replaces the current remote-inventory operation enum and its request/apply
messages, so the wire changes and `ProtocolVersion.Current` is bumped in the same change (stage 1),
per the numbering policy in `docs/decisions/active.md`. The handshake check is the compatibility
boundary; no dual shape is kept.

### 3.6 What is deleted, and where the new code lives

- Deleted with the clone-edit path: `PlayerRemoteInventoryService`'s mirror-edit halves (drop, move
  to container, pour, their kernel command builders and item-tree helpers), the CUO operation enum,
  and the `RemoteBackpackOperationHandler` gesture table.
- `PlayerRemoteInventoryService` keeps only the host half of this design: validate, arbitrate,
  record, and forward the intent to the owner. Its file stands at 595 lines today, so the change is
  measured before it is edited and the file is split if the host half plus its plumbing still crosses
  the architecture line gate.
- New adapter code: the release-window capture (the proxy decision plus the mutation-call
  interception) and the owner-side intent executor that replays the native call. Patches report only
  verified writes, so an intercepted call that produced no intent must not be reported as one.
- The owner-side apply path is REPLACED, not extended: `GameAdapter/RemoteInventoryOperationApply.cs`
  — which today replays seven kinds and passes `force: true` to `Body.PickUpItem`, bypassing the
  native guards the owner is supposed to inherit — goes with `RemoteInventoryApplyMsg` and its
  runtime handler, and the intent executor takes its place.
- **The suppressions that have to come off.** The design depends on the native branch running, and
  today three patches hold it shut: `Patches/PlayerCameraDragUsePatch.cs` owns the ordering table and
  the proxy-release cancel (only the window bracket and the proxy decision survive);
  `Patches/PlayerCameraHandleWhileDraggingPatch.cs` skips the whole while-dragging body while the
  remote view is open, which is exactly why the native drain tick and the favourite toggle cannot
  happen; `Patches/PlayerCameraTryPerformRadialActionPatch.cs` answers R10 with a flat "not
  consumed", so `UseItem` and `WearWearable` can never become intents; and `RemoteProxyDragPolicy`'s
  release-cancel rule is absorbed by the window's own fail-closed case. Stage 1 removes the first and
  the last, stage 2 restores the while-dragging body, stage 3 the radial branch.

## 4. Stage plan

| Stage | Scope | Exit |
|---|---|---|
| 0 | This record: mechanism inventory, intent vocabulary, ticket adjudication, decision record | design frozen before code |
| 1 | Inventory and slot family: move, swap, transfer to the requester, drop, take-out; window + capture seam; owner-side executor; host validate/arbitrate/record; clone-edit path deleted; protocol bumped | reported behaviour 1 and 2 no longer reproduce; intent tests green |
| 2 | Containers: move into and out of a container, nested containers, the trash bag, the while-dragging drain, the container-window gesture; the vanish case is a regression test here | matrix rows 3-5 |
| 3 | Item interactions: use, wear, combine, battery, favourite, and the held-remote-item chain (close the backpack, use the held item from the medical panel) | matrix rows 6-8 |
| 4 | Family audit and acceptance: both directions, a third peer, worn items, containers and the craft screen; the display rows carried over from the absorbed tickets | the rework ticket's matrix |

## 5. Adjudicated tickets

| Ticket | Verdict | Reason |
|---|---|---|
| `docs/backlog/review/unified-remote-display-projection-rework.md` | **stays landed** (not reopened) | its seam (`RemoteCharacterDisplayProjection`, `RemoteItemPresentation.ApplySourceValues`) is exactly the display half this design keeps; nothing in this rework replaces it |
| `docs/backlog/review/global-projection-framework.md` | **stays landed** (not reopened) | the projection contract is untouched: the viewer's backpack is still a rebuildable read model of the owner's facts |
| `docs/backlog/resolved/remote-backpack-native-interaction-parity.md` | **absorbed** into the rework ticket | it is the rejected delivery this ticket replaces; its operation map described the routing table that §3.2 deletes |
| `docs/backlog/resolved/remote-backpack-item-projection-acceptance-issues.md` | **absorbed** into the rework ticket | its reported rows are the rework's acceptance input; the two rows that were about display only (durability source values, container content rendering) are the landed projection seam's, and its interactive rows are matrix rows 2-9 |

## 6. Recorded limits of this stage

- No behaviour changed: the design is not evidence that any reported reproduction is fixed.
- The mechanism inventory covers the drag pipeline of `PlayerCamera`. The medical-panel and
  context-menu interaction families reach the same native mutation calls through different UI paths;
  stage 3 audits them against the same capture seam instead of assuming they are covered.
- The two-client feel (native animation, sound and timing on the operator's screen) remains the
  user's release-cycle acceptance; this stage produces no runtime evidence for it.

## Related reading

- [Backlog index](../backlog/README.md) — where this work sits in the queue.
- [Remote inventory native parity rework](../backlog/todo/remote-inventory-native-parity-rework.md) — the ticket this design serves.
- [Current architecture](current.md) — the kernel and authority model the host half rides.
- [Projection framework](projection-framework.md) — the display half this design leaves in place.
- [Active decisions](../decisions/active.md) — the protocol numbering policy and this cycle's decision.
