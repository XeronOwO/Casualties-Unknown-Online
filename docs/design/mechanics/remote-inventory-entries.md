# Remote inventory: the entry table

The gestures a player performs on **another player's** inventory, and the native call each one is. This page is the reference the intent names
are keyed to: a remote operation travels as the native call it replays, so the table below IS the vocabulary. It was extracted from
`docs/architecture/remote-inventory-native-parity.md` §2 and §3.3 when that page was retired into this corpus.

## What the player does

Drags with the `iteminteract` bind over somebody else's displayed backpack: picks an item up out of it, moves one inside it, drops one onto a
slot or a container, uses or wears one from it, loads a battery into it, pours from it, hands it to a trader, or releases their OWN item onto it
(a GIVE). The expectation is always the same: the thing happens exactly as it would in their own inventory, with the game's own sounds and
animations, exactly once.

## What the game itself does

The game's drag has three phases, and the classification lives in the release's **hit-test dispatch**, not in a table (`PlayerCamera.cs`):

```text
GetKeyDown(iteminteract) -> HandleStartDragging      (PlayerCamera.cs:1357)
GetKeyUp(iteminteract)   -> HandleReleaseDragging    (PlayerCamera.cs:1361)
every frame              -> HandleWhileDragging      (PlayerCamera.cs:1363)
```

**Drag start** decides only what is being dragged and mutates nothing: a hovered inventory label (`:1405`, `this.dragItem = itemLabel.refItem`), a
hovered inventory button that passes its own overlap filter (`:1411`, `this.dragItem = invButton.GetItem()`), a world item (`:1437`), a player
collider (which opens the radial instead), or a usable object in range (`:1449`).

**While dragging** two operations are CONTINUOUS, not release branches: the water container's drain over the drain object (every frame,
`:1729`) and the favourite bind over an inventory button (`:1745`, which writes the HOVERED item's field, not the dragged one).

**Release** walks the UI raycasts in order and stops at the first hit that answers "consumed": `DoPickupCheck(dragItem, false)` (`:1467`), then
`TryPerformUIActions` (`:1500`) → `TryPerformInventoryAction` (`:1529`), `TryPerformRadialAction` (`:1636`), `TryPerformSpecialUIAction` (`:1654`),
then the world fallbacks (`:1686`). Every branch:

| # | Native condition | Native call | Anchor |
|---|---|---|---|
| R1 | the hit's `InvButton` holds the dragged item | nothing — consumed | `:1536` |
| R2 | hit item has a battery, dragged item has tag `tool` | `hitItem.battery.UnloadBattery(false)` | `:1543` |
| R3 | hit item has a battery, dragged item has tag `battery` | `hitItem.battery.LoadBattery(dragItem)` | `:1548` |
| R4 | hit item is a `Container` | `container.UnloadItem(dragItem, null)` then `container.LoadItem(dragItem)` | `:1567` |
| R5 | hit item is a `Container`, `expanddesc` held, hit is not the dragged item | the same pair for every direct child that `CanHoldItem` | `:1585` |
| R6 | `body.CanCombine(hitItem, dragItem)` | `body.CombineItems(hitItem, dragItem)` | `:1600` |
| R7 | hit is a body slot, the item is wearable and not `wearableCanBeHeld` | native alert only, consumed | `:1609` |
| R8 | hit is a body slot, both are held items | `body.SwapSlots(hitSlot, body.SlotOf(dragItem))` | `:1614` |
| R9 | hit is a body slot, otherwise | `body.DropItem` / `PickUpItem(dragItem, hitSlot, false)` | `:1621` |
| R10 | hit is tagged `RadialCenter`, the radial is open, the pointer is inside | `body.WearWearable` or `body.UseItem`, consumed | `:1640` |
| R11 | hit carries a `WoundViewLimb` | `ApplyWoundItem` → `useLimbAction` / `ApplyToLimb` | `:1658`, `:739`, `:750` |
| R12 | hit is tagged `TradeThing`, a trader is open | `currentTrader.GiveItem(dragItem)` | `:1661` |
| R13 | hit is tagged `ContainerBack`, a container window is open | the container pair again | `:1666` |
| R14 | hit is the craft button | `OpenCraftScreen()` + `SeeRecipesWithItem(dragItem)` | `:1673` |
| W1 | nothing consumed, the item's parent is a `Container` | `container.UnloadItem(dragItem, null)` | `:1689` |
| W2 | nothing consumed, held, wound view closed, pointer moved > 10 px | `body.DropItem(dragItem)` | `:1694` |
| W3 | same, the item is worn by the body | `body.DropWearable(dragItem)` | `:1698` |
| W4 | same, a world `Container` under the pointer, the item is not a container | the container pair | `:1704` |

**The guards live inside the callees and read the local scene**, which is why the owner must execute: `Container.CanHoldItem` (weight and tag
restriction, `Container.cs:110`), `Container.LoadItem` (refuses a nested container that still holds something, a container inside a container,
itself, and requires `CanHoldItem` plus a distance below 10 units, `:116`), `Container.UnloadItem` (`:154`), `Body.DoPickupCheck` (`Body.cs:1356`),
`Body.PickUpItem` (`:1388`, which also unloads the parent container and plays the backpack sound), `Body.SwapSlots` (`:1413`).

## Rules and states

- **The capture is a CALL bracket, not a time bracket.** CUO opens a window when the dragged item is a display proxy — a RELEASE window in
  `HandleReleaseDragging`, a WHILE-DRAGGING window per frame for the two continuous sites — and closes it in the same patch's finalizer, so a
  throwing native body cannot leak a window into the next frame. Inside the window the native mutation calls above are recorded and their originals
  skipped: the game's ordering, predicates and early exits all run, and CUO owns no classification table. A projection rebuild, a packet pump or any
  other Unity callback cannot be captured by construction, because each window spans exactly one native invocation.
- **The bracket answers the body-shaped predicates from the body the RING shows**, because the native branch reads the LOCAL body while the ring and
  the dragged item belong to the displayed clone. Without that, R8's swap guard never passes, the world fallbacks never fire, R9 resolves a slot of
  the requester's own body, and the release's first gate measures the distance between the local body and a proxy standing where the remote player
  stands — dropping the whole release silently. Every such answer happens BEFORE the game's own body runs, because those bodies keep reading the
  local state behind the guard that answered them (decision 218).
- **One native mutation call is one intent.** The wire carries the call's identity plus its operands, keyed by authoritative instance ids — never
  a CUO taxonomy and never a mirrored result. The members are the calls above: move into a container, take out of one, the container-children
  loop, pick up to a slot, swap slots, drop, drop a wearable, use, wear, combine, load a battery, unload a battery, drain (carrying the AMOUNT
  the frame removed, never the viewer's stack list), apply to a limb, hand to a trader (position-keyed, because the owner has no trader open).
- **Two pairs are the same intent read from both ends.** The cross-player transfer has no single native call: `TransferToBody` is the native
  release on the owner's side plus the native pickup on the destination side, and `GiveToBody` is the same pair read from the giver's end; the
  host decides the item id first-writer-wins between them.
- **The unload/load pair on the SAME container is one move**; an unload with no matching load on its own container is a take into the world. The
  merge is per container on purpose: a take-out of one container followed by a move into a DIFFERENT one is two intents, because the native code
  never makes the take-out conditional on the second container accepting.
- **R5's loop is ONE intent**, resolved on the owner: the native source of the children is the dragged item's OWN container component, so one
  intent per child would make the owner run the loop on a container the first run already emptied.

## Refusals

- A native guard's refusal is the native refusal: the owner reports it, the host records it, and one observable line names the intent, the item
  and the native reason (container full, blocked slot, `onlyHoldInHands`).
- A release the dispatch consumed that produced no intent is logged as an unclassified gesture — EXCEPT the four classified native no-ops (R1,
  R7, R14 and R10's release for an item that is neither wearable nor usable). A gesture CUO has never seen must be observable, never a silent
  no-op.
- A world item released onto a displayed backpack keeps its named refusal (`give-and-take.md`): the hand-over is stated to name an item the giver
  carries.
- A destination body that is neither the owner nor the requester is refused by the host with one logged line — that is how cross-player
  remote-to-remote handoff stays out of scope without becoming a silent no-op.

## Who judges

The requester's client decides the gesture and the intent; the host decides permission, ownership and first-writer-wins arbitration; the owner's
client performs the native call with its real guards and reports the result. The permission itself is `RemoteBackpackAccess` (decision 263), and
the three roles' boundaries are stated on `../model/item-custody.md`.

## What travels

The intent (the native call identity plus operands) and the authoritative result. The requester never mutates a display proxy, and the owner never
executes an intent the session did not admit.

## What the player sees and hears

The branch-level feedback the game's own branch makes runs on the requester's client (backpack sound, alerts, slot scale, radial and container-window
state), while a sound or animation produced INSIDE a mutation call happens where the mutation happens — on the owner's client. The limits that
remain are on `../model/item-custody.md`'s `Gaps`.

## Records

The stage decisions 218, 219, 220 and 221; decision 258 for the favourite mark; decision 263 for `RemoteBackpackAccess`; decision 240 for the
accepted cross-player release-animation limit. The ticket this table came with is
[`done/remote-inventory-native-parity-rework.md`](../../backlog/done/remote-inventory-native-parity-rework.md), the stage-4 audit that traced
every row to its mechanism is
[`remote-inventory-native-intent-stage4-audit.md`](../../evidence/selfchecks/items/remote-inventory-native-intent-stage4-audit.md), and the
display half this page leaves untouched is
[`projection-framework.md`](../../architecture/projection-framework.md).

## Gaps

- The table is the `PlayerCamera` drag pipeline; the medical panel's and the craft screen's own mutation calls were audited against the same
  capture seam (stage 3) and are recorded as audit results rather than as coverage.
- `UIUtil.IsPointerOverContextMenu` exists in the build and no code calls it or sets its tag, so that half of the context-menu family has no
  counterpart to cover; a prefab-carried tag cannot be ruled out from the decompiled tree.
