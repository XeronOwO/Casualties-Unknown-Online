# Remote Inventory Operations: Native Parity Design

Status: **stage 4 landed** — the native intent path (stage 1, decision 218), the container family
and the while-dragging body (stage 2, decision 219), the item interactions (stage 3, decision 220) and
the family audit with the acceptance preparation (stage 4, decision 221): R10's radial-centre use and
wear, combine, the battery load/unload pair, the while-dragging favourite store and the trader hand-in,
each of them an intent the owner replays on its real items, with the acceptance matrix audited row by
row. The audit changed no behaviour; its verdicts, the two re-evaluated projection tickets and the
three limits the run has to judge are in
`docs/evidence/selfchecks/items/remote-inventory-native-intent-stage4-audit.md`, and the run itself —
both directions, a third peer, worn items, containers and the craft screen on the real machine — is the
user's release-cycle action, with its steps in
`docs/evidence/selfchecks/items/remote-inventory-native-parity-acceptance-checklist.md`.

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

1. **Two bracket kinds, each spanning exactly one native invocation.** `HandleReleaseDragging` opens
   a **release window** when the dragged item is a remote display proxy, and `HandleWhileDragging`
   opens a **while-dragging window** for each frame the two continuous sites of §2.2 need one. Both
   are closed by the same patch's finalizer, so a throwing native body cannot leak a window into the
   next frame. A window is a **call bracket, not a time bracket**: it spans exactly one native
   invocation, so the only calls inside it are the ones the native body itself makes. A projection
   rebuild — `CloneInventoryRenderer` calls `Container.LoadItem` and
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
   does — `Container.LoadItem`'s refusal leaves the item exactly where the take-out put it. **One
   native loop is one intent (stage 2)**: R5's container-expansion loop unloads and loads every direct
   child of the dragged container's own container, and the whole loop is ONE
   `MoveContainerChildren` intent — the native source of the children is `dragItem.container`, the
   dragged item's OWN container component, so the owner enumerates them and its own
   `Container.CanHoldItem` decides which of them move. One intent per child would make the owner run
   the loop once per child, on a container the first run has already emptied.
5. The window fails closed and loud: a release that the native dispatch consumed but that produced no
   intent is logged as an unclassified gesture. A native gesture CUO has never seen must be
   observable, never a silent no-op. **Stage 3 adds one classified no-op to that list**: R10 consumes
   a release for an item that is neither wearable nor usable without running anything at all, and the
   radial probe records the native method's own `true` answer for it — the window never re-derives
   that branch's geometry to recognise the case.
6. **The branch's guards are answered by the body the ring shows (implementation amendment, stage 1).**
   The inventory in §2 assumed the native branch classifies a display proxy the way it classifies a
   real item. It does not. The branch reads `PlayerCamera.body`, which is always the LOCAL body, while
   the inventory ring and the dragged item belong to the displayed clone (`InvButtonBodyPatch`
   redirects every `InvButton`'s body). A proxy is therefore never "held" or "worn" by the body the
   branch asks; R8's swap guard never passes; the world fallbacks W2/W3 never fire; R9's
   occupying-slot step resolves a slot of the requestER's own body; and the release's first gate,
   `Body.DoPickupCheck(dragItem, false)`, measures the distance between the local body and a proxy
   standing where the remote player stands — it would drop the whole release silently. The stage 1
   window therefore carries three additions, and they are what keeps the classification the game's
   own instead of a CUO table: (a) while the bracket is open, the predicates the branch reads —
   `HoldingItem`, `GetItem`, `GetWearable`, `DoPickupCheck` — are answered from the displayed body,
   so R8's guard, R9's own steps and W2/W3 see the proxy exactly as that body does; (b) the calls the
   branch then makes are captured under the pair rules of §3.2.4; and (c) the steps internal to a
   captured call (R9's two drops, the `unload` half of a container move) are absorbed into that
   call's intent rather than executed twice, on the wrong body.
7. **The while-dragging frame is its own bracket kind (implementation amendment, stage 2).** The
   while-dragging body reads the local scene as well — `this.body.DoPickupCheck(this.dragItem, true)`
   is the drain tick's gate — so a proxy drag opens the same window under the while-dragging kind for
   each frame, the pickup check is answered for the dragged proxy exactly as in a release bracket, and
   the frame's mutations are captured instead of running on a proxy. The favourite toggle was the one
   exception in stage 2 — a direct `favourited` field write on the HOVERED item, which no call seam
   takes, so that frame was skipped with one logged line — and **stage 3 builds the seam that observes
   it instead (§3.2.8)**, so no frame is skipped for it any more.
   The drain tick is the frame's other mutation and it is captured with the AMOUNT the frame removed,
   never the per-stack list the viewer computed from its proxy: the owner re-derives the distribution
   from the stack its own item has, so a stale proxy stack cannot leak into the owner's item. A proxy
   the bracket did NOT take (it carries no authoritative identity, or the bracket belongs to another
   item) keeps the native body running for its own feedback while the seam refuses its drain call and
   reports it once per dragged proxy — no path may mutate a display proxy, and the release path fails
   closed for the same shape.
8. **The one mutation with no call behind it is observed across the frame bracket (implementation
   amendment, stage 3).** The favourite store has no seam Harmony can take without rewriting the
   method's IL, and the stage-2 answer — skip the frame — cost that frame's drain tick and produced no
   intent. The frame bracket is the seam instead: the while-dragging patch reads the `favourited` value
   of every inventory button the frame's own raycasts reach before the native body runs and compares it
   afterwards, so the native condition (`GetKeyDown(favourite)` on the first overlapping button,
   `PlayerCamera.cs:1736-1747`) still decides whether a store happens and CUO only turns the store it
   observes into `ToggleFavourite` (§3.2.3's rule that the classification is the game's own holds: no
   CUO replica of that condition exists). The observed proxy's field is put back to the value the frame
   started with — no path may mutate a display proxy — and the projection already carries `favourited`
   as a source value (`RemoteItemPresentation.SourceValues`), so the operator's star lights up from the
   owner's own answer rather than from a predicted one. A flipped proxy the bracket did not take (no
   authoritative identity, or no open bracket) is put back and reported once, exactly like the drain
   seam, and a flipped LOCAL item is left alone — that store is the local gesture, unchanged by the
   remote view being open.

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
| `CombineItems` | `body.CombineItems(target, item)` (R6) | item, target item (the FIRST argument — the receiver) |
| `LoadBattery` | `target.battery.LoadBattery(item)` (R3) | item (the battery), target item |
| `UnloadBattery` | `target.battery.UnloadBattery(false)` (R2) | target item |
| `ToggleFavourite` | the `favourited` write (§2.2) | item (the HOVERED one) |
| `Drain` | `WaterContainerItem.Drain(amount)` (§2.2) | item, amount |
| `ApplyToLimb` | `ApplyWoundItem` → `useLimbAction` / `ApplyToLimb` (R11) | item, limb |
| `GiveToTrader` | `currentTrader.GiveItem(item)` (R12) | item, trader position |
| `TransferToBody` | the two-sided transfer: the owner releases (`Container.UnloadItem` / `Body.DropItem`), the destination body runs R9's own sequence — drop the occupying slot item when the destination slot is held, then `Body.PickUpItem(item, slot, false)` | item, destination body, slot |

`TransferToBody` is the one intent with no single native call behind it, because the native world has
no cross-player transfer: its two halves are the native release on the owner's side and the native
pickup on the destination side, and the host arbitrates the item id first-writer-wins between them.
R1 and R7 produce no intent (they are consumed native no-ops), and R14 plus the container-window
open of §2.3 are local UI on the viewer and never travel. **Stage 3 corrects one operand row**: R12's
native call reads `PlayerCamera.currentTrader`, and the OWNER's client has no trader open, so the
trader itself is an operand — the intent carries its world position, the identity the trade domain
already keys its messages by (`TraderSwingMsg.Position`, `TradeStateSync.FindTraderAt`).

**Stage 1 carried eight of these members** — `DropItem`, `DropWearable`, `TakeOutOfContainer`,
`MoveIntoContainer`, `SwapSlots`, `PickUpToSlot`, `TransferToBody` and `ApplyToLimb` — because they
are what the release branch can produce once the suppressions of §3.6 come off. **Stage 2 adds
`MoveContainerChildren`** (R5, with the container-expansion gesture) **and `Drain`** (the
while-dragging drain tick, with the restored while-dragging body). **Stage 3 adds the seven
item-interaction members** — `UseItem`, `WearItem`, `CombineItems`, `LoadBattery`, `UnloadBattery`,
`ToggleFavourite` and `GiveToTrader` — together with the message's second item operand
(`TargetItemInstanceId`) and its trader operand, and `ProtocolVersion.Current` is bumped in the same
change (37 → 38, the handshake stays the compatibility boundary). No member of the vocabulary is
refused any more: every gesture §2.2 and §2.3 list now becomes an intent the owner replays.

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

**The stage-3 audit, with the sync inventory as its evidence.** The existing paths carry item STATE
(condition, `favourited`, liquids, container contents — the item events plus the periodic character
snapshot), the player-character one-shots (P5 `CharacterSound`, whose policy classifies only its own
call-identity scopes and the direct placeable uses), and a handful of world-mechanism sounds replayed
by hand at their own event's replay site (the battery charger's `batteryinsert`). They do **not** carry
an item's own sound: `combine` and `waterpour` (`Body.CombineItems` / `Body.CombineLiquids`),
`batteryinsert` (`BatteryItem.LoadBattery` / `UnloadBattery`) and `eatFlesh` / `eatCrunch` / `drink`
(the `Stats.useAction` delegates) are `Sound.Play` calls *inside* the mutation, so they are heard by
whichever client runs the call — today for every player, in a session, not only for the
remote-inventory case. The operator's screen therefore keeps the native branch-level feedback (the
ring, the cursor, the drag image, the favourite key's own UI click, the alerts, and the backpack sound
the container branches play at dispatch level) and the authoritative result through the projection,
while the mutation-internal sound and animation happen where the mutation happens, on the owner's
client. Re-sending those sounds needs a channel that does not exist and would change local-versus-
remote for every player, so it is recorded as a limit (§6) instead of being invented here.

### 3.5 Protocol

The intent vocabulary replaces the current remote-inventory operation enum and its request/apply
messages, so the wire changes and `ProtocolVersion.Current` is bumped in the same change (stage 1),
per the numbering policy in `docs/decisions/active.md`. The handshake check is the compatibility
boundary; no dual shape is kept.

### 3.6 What is deleted, and where the new code lives

- Deleted with the clone-edit path (stage 1): `PlayerRemoteInventoryService`'s mirror-edit halves
  (drop, move to container, pour, their kernel command builders and item-tree helpers) together with
  the service itself, `RemoteBackpackOperationHandler`'s gesture table, `RemoteInventoryOperationApply`
  and the operation enum with its request/apply message pair.
- The host half that replaced it is `PlayerRemoteInventoryIntentService` (269 lines against the
  deleted service's 595): validate, arbitrate first-writer-wins per item instance, forward to the
  owner. The split this section planned was not needed — the measurement above the architecture line
  gate is part of the change, which is why the deleted service is gone rather than kept beside its
  replacement.
- New adapter code: the release-window capture (the proxy decision plus the mutation-call
  interception) and the owner-side intent executor that replays the native call. Patches report only
  verified writes, so an intercepted call that produced no intent must not be reported as one.
  **Stage 3 widened both halves**: the interception now covers `Body.UseItem` / `Body.WearWearable`
  (R10), `Body.CombineItems` (R6), `BatteryItem.LoadBattery` / `UnloadBattery` (R2/R3) and
  `TraderScript.GiveItem` (R12), the while-dragging patch observes the `favourited` store across its
  frame bracket, and the executor replays all seven kinds on the owner's real items. The report-hook
  audit that came with it found two hooks that would have reported a call the window took:
  `UseItemPatches`' `OnItemUsed` (which would have stated a use of an item the operator never touched,
  and stamped an instance id on a proxy doing it) and `WearWearablePatch`'s unconditional
  "re-report right away" — both now skip a display proxy, and `TraderPatches.GiveItemPatch` reports
  only a hand-in whose credit really moved, which the window's capture makes mandatory (the
  requester's captured call would otherwise have credited the trader for an item nobody gave).
- The owner-side apply path is REPLACED, not extended: `GameAdapter/RemoteInventoryOperationApply.cs`
  — which today replays seven kinds and passes `force: true` to `Body.PickUpItem`, bypassing the
  native guards the owner is supposed to inherit — goes with `RemoteInventoryApplyMsg` and its
  runtime handler, and the intent executor takes its place.
- **The suppressions that have to come off.** The design depends on the native branch running, and
  three patches held it shut: `Patches/PlayerCameraDragUsePatch.cs` owned the ordering table and
  the proxy-release cancel (only the window bracket and the proxy decision survive);
  `Patches/PlayerCameraHandleWhileDraggingPatch.cs` skipped the whole while-dragging body while the
  remote view is open, which is exactly why the native drain tick and the favourite toggle could not
  happen; `Patches/PlayerCameraTryPerformRadialActionPatch.cs` answered R10 with a flat "not
  consumed", so `UseItem` and `WearWearable` could never become intents; and `RemoteProxyDragPolicy`'s
  release-cancel rule is absorbed by the window's own fail-closed case. Stage 1 removed the first and
  the last; stage 2 restored the while-dragging body, with its favourite write still refused and
  logged (§3.2.7) until the item-interaction stage built that seam; **stage 3 removed the third and
  replaced it with the radial probe** (`Patches/PlayerCameraRadialActionProbePatch.cs`, which only
  records the native branch's own no-op answer for §3.2.5) **and replaced the favourite refusal with
  the frame-bracket observation of §3.2.8**, so no gesture of §2.2/§2.3 is suppressed any more.

## 4. Stage plan

| Stage | Scope | Exit |
|---|---|---|
| 0 | This record: mechanism inventory, intent vocabulary, ticket adjudication, decision record | design frozen before code |
| 1 | Inventory and slot family: move, swap, transfer to the requester, drop, take-out; window + capture seam; owner-side executor; host validate/arbitrate/record; clone-edit path deleted; protocol bumped | **landed** (decision 218): the reported drop and slot rows no longer route through a host mirror edit; the window, the replay and the host half are covered by intent, window-state and host-contract tests |
| 2 | The container-expansion gesture (R5), nested containers and the trash bag, the while-dragging drain tick and the container-window refresh; the vanish case is a regression test here | **landed** (decision 219): R5 is one `MoveContainerChildren` intent the owner evaluates on its own children, the while-dragging body runs again with each frame's drain tick as one `Drain` intent carrying the amount, and the vanish case is pinned by a regression on the host's copy of the owner's inventory; matrix rows 3-5 are code facts here — the operating feel stays the user's acceptance run |
| 3 | Item interactions: use, wear, combine, battery, favourite, the trader gesture and the held-remote-item chain (close the backpack, use the held item from the medical panel) | **landed** (decision 220): the seven item-interaction kinds ride the stage-1 seam with the message's second item operand and its trader operand (`ProtocolVersion.Current` 37 → 38), R10's use/wear branch runs again, the favourite store is observed across the frame bracket, and matrix rows 6-8 are code facts here — the operating feel, the item sounds the operator does not hear (§3.4) and the medical chain's real-machine behaviour stay the user's acceptance run |
| 4 | Family audit and acceptance: both directions, a third peer, worn items, containers and the craft screen; the display rows carried over from the absorbed tickets | **landed** (decision 221): every matrix row is traced to its mechanism and its test, the craft screen is proved unable to reach a display proxy, the container-window re-bind and the owner-side worn-item resolution are confirmed, the two directions are shown to share one validation path, and the three remaining limits are named as the acceptance run's judgement items — no behaviour changed, and the run itself stays the user's |

## 5. Adjudicated tickets

| Ticket | Verdict | Reason |
|---|---|---|
| `docs/backlog/review/unified-remote-display-projection-rework.md` | **stays landed** (not reopened) | its seam (`RemoteCharacterDisplayProjection`, `RemoteItemPresentation.ApplySourceValues`) is exactly the display half this design keeps; nothing in this rework replaces it |
| `docs/backlog/review/global-projection-framework.md` | **stays landed** (not reopened) | the projection contract is untouched: the viewer's backpack is still a rebuildable read model of the owner's facts |
| `docs/backlog/resolved/remote-backpack-native-interaction-parity.md` | **absorbed** into the rework ticket | it is the rejected delivery this ticket replaces; its operation map described the routing table that §3.2 deletes |
| `docs/backlog/resolved/remote-backpack-item-projection-acceptance-issues.md` | **absorbed** into the rework ticket | its reported rows are the rework's acceptance input; the two rows that were about display only (durability source values, container content rendering) are the landed projection seam's, and its interactive rows are matrix rows 2-9 |

## 6. Recorded limits of this stage

- The mechanism inventory covers the drag pipeline of `PlayerCamera`; the medical-panel and
  craft-screen paths were audited against the same capture seam in stage 3 and the audit's result is
  recorded under the stage-3 limits below.
- The two-client feel (native animation, sound and timing on the operator's screen) remains the
  user's release-cycle acceptance; no automated test can produce it. Stage 3 sharpens that: the item's
  own sound is one of the things the operator does not hear (§3.4), and whether that is acceptable on
  the real machine is the user's call.
- Stage 1 leaves, deliberately and observably:
  - The report hooks that answer "what did this write do?" must never observe a redirected
    predicate: `BodyItemPatches`' pickup/drop/swap reports, `PickupSync.OnPickedUp` and
    `ItemSlotSync.OnSlotMoved` all skip a display proxy (or a call the window took), because a
    proxy has no `ItemInstanceId` and the id-less branch would allocate it a fresh one — the
    "extra item" family this repository has hit before.
  - A proxy released onto the radial centre or held over the drain is refused with one Information
    line (the radial release is consumed, so the world fallbacks cannot turn it into a drop or a
    take-out nobody asked for); the drain report is rate-limited to one line per dragged proxy.
    **Stage 3 removed the radial half of this**: R10 runs again, so a release inside the circle is a
    use, a wear, or the native no-op of §3.2.5 — and a release outside the circle falls through to the
    world fallbacks, which is what it does locally.
  - A release that produced no intent is only logged as an unknown gesture when it is none of the
    three classified native no-ops (R1 its own slot, R7 a wearable that cannot be held, R14 the
    craft button). **Stage 3 added a fourth: R10's consumption of a release that has no action to run
    (§3.2.5).**
  - A display proxy that carries no authoritative identity cancels its drag before the native body
    runs, which is the fail-closed rule the deleted release-cancel policy held.
  - A local item released over an in-world remote player keeps the landed cross-player use route
    (`PatchBridge.TryHandleDraggedItemUseOnRemote`), which the window patch calls before the native
    body.
  - `TransferToBody` and `ApplyToLimb` still run on their landed host-authoritative paths (the
    custody transfer's snapshot edit plus the transfer event, and the cross-player use path). The
    owner-side native release animation for the custody move is not implemented; it belongs to the
    family audit.
  - W4 (a world container under the pointer) resolves that container on the owner's side by instance
    id. A container the owner's scene cannot resolve is refused with one logged line.
  - A LOCAL item dragged while the remote backpack view is open now runs the native branch: it is no
    longer cancelled the way the deleted ordering patch cancelled it, and its slot operand comes from
    the ring (the owner's body). That corner is recorded here rather than silently changed.
  - The remote-inventory presentation (`RemoteItemPresentation`, `CloneInventoryRenderer`) is
    untouched: this stage changes interaction only.
  - Stage 2 leaves, deliberately and observably:
    - The drain is the one CONTINUOUS intent: it arrives once per frame, so it does NOT trigger the
      per-intent immediate character re-report the discrete kinds trigger — sixty full reports a
      second is not a cadence. Un-evented continuous item state (decay, battery charge, liquid)
      already rides the periodic character snapshot, so the drained level reaches the other clients at
      that cadence while the owner's own screen drains natively. Its happy path logs at Debug for the
      same reason; a refusal keeps its Information/Warning line.
    - The favourite toggle through the remote view stays refused with one Information line per dragged
      proxy. The window cannot take it: the native branch writes the `favourited` FIELD of the hovered
      item, so there is no call to intercept, and the frame that would write it is skipped instead.
      The intent and its store-level seam arrive with the item-interaction stage. **Stage 3 built that
      seam (§3.2.8): the frame runs, and the store it observes becomes `ToggleFavourite`.**
    - The children of an expansion batch are enumerated on the OWNER. The requester's projection
      decides only whether the native loop runs at all (its own `CanHoldItem` reads the clones), and
      the owner's guard then decides per child which items actually move — the native partial outcome,
      where a child the guard refuses stays where it is and the rest still move.
    - The radial weight readout (`PlayerCamera.HandleRadialMenu`) still prints the LOCAL body's
      encumbrance while the remote ring is open, because the native code reads `PlayerCamera.body`.
      That is a display detail outside this stage's interaction scope and it is recorded for the stage
      4 family audit rather than changed here.
    - The pickup-feasibility gate (`Body.DoPickupCheck`) is answered for the dragged proxy in BOTH
      bracket kinds, from one pure predicate on the window
      (`RemoteDragIntentCapture.AnswersPickupCheckFor`) rather than from the seam that happens to need
      it: the release branch reads it first (`PlayerCamera.cs:1467`) and the drain tick reads it again
      every frame (`:1729`), and both compare the local body against an item standing where the remote
      player stands. A proxy that is not the dragged one, or that carries no authoritative id, keeps
      the native answer.
    - Holding the favourite key over a remote inventory button skips that ONE frame's native
      while-dragging pass — the only way to stop a field store Harmony cannot intercept — so that
      frame's drain tick is skipped with it, and the refusal line names that cost. One frame per key
      press; the frame bracket still never merges ticks across frames. **Stage 3 removed that cost:
      the frame runs natively and only the field store it observes becomes an intent (§3.2.8), so the
      frame's drain tick survives a favourite key press.**
    - A continuous intent refreshes the item's first-writer-wins lease for as long as the gesture runs
      (the operator holds the item, which is the lease's purpose), and a competing continuous intent's
      refusal logs at Debug: the lease's original rationale — the owner's next report normally arrives
      well inside it — does not describe a kind that deliberately does not re-report per frame.
  - Stage 3 leaves, deliberately and observably:
    - **The item sounds inside a replayed call are heard where the call runs.** §3.4's audit found no
      existing path that carries an item's own sound, so `combine`, `waterpour`, `batteryinsert` and
      the `useAction` eating/drinking clips play on the owner's client — the client that performs the
      mutation — while the operator's screen keeps the branch-level native feedback and the
      authoritative result through the projection. Carrying them would need a channel that does not
      exist and would change local-versus-remote for every player, so it is recorded rather than
      invented here; the operator-side item sound is therefore a real-machine acceptance question.
    - **The radial-centre release is now the native branch's own answer, including its no-op.** R10
      consumes a release for an item that is neither wearable nor usable without running anything; the
      probe records that as a classified no-op instead of letting it read as an unclassified gesture
      (§3.2.5), and a pointer that is NOT inside the circle falls through to the world fallbacks
      exactly as it does locally — so a proxy can now be dropped by that path, which is the native
      meaning of the gesture and no longer a refusal.
    - **One use/wear release can be two intents.** The native branch tests `wearable` and `usable` as
      independent `if`s, so an item carrying both flags produces `WearItem` followed by `UseItem` in
      the native order; the owner replays them in that order.
    - **The trader hand-in is position-keyed**, because the owner's client has no trader open: the
      intent carries the trader's world position and the owner resolves its own same-position trader
      with the trade domain's tolerance (`TraderLocator`). An owner whose scene has no trader there
      refuses with one line, and the trader's own credit total is the verified write — a hand-in the
      trader refuses (a container that still holds something, a valueless or already-bought item, the
      lifetime credit cap) credits nothing and is logged as the native refusal it is.
    - **The medical-panel and craft-screen paths were audited, not assumed.** Every mutation call of
      this family has exactly one caller outside `PlayerCamera`'s drag pipeline: the touch auto-wear
      (`Body.cs:527`, which cannot reach a display proxy — a proxy is parented to the clone, never a
      world item), and the craft screen's own consumption and battery calls (`Recipe.cs:179`,
      `RecipeItem.cs:163`, `RecipeResult.cs:67`), which are local UI on the viewer and whose facts ride
      the landed craft report; the medical panel reaches these calls only through
      `PlayerCamera.ApplyWoundItem` (R11, captured by the release window) and the landed
      host-authoritative medical-operation session. The design's §6 note also named a context-menu
      family: `UIUtil.IsPointerOverContextMenu` is defined in the game build and **no** code calls it
      or sets the tag, so that half has no counterpart to cover — a prefab-carried tag cannot be ruled
      out from the decompiled tree, which is why this is recorded as an audit result and not as
      coverage.
    - **A crafted/consumed item's ids still come from the landed paths.** `BatteryItem.UnloadBattery`
      creates a battery item on the owner's body and `AutoPickUpItem` hands it to the body; the
      immediate authoritative re-report stamps and states it like every other carried item
      (`CarriedInventoryReporter`), which is why the new kinds need no id plumbing of their own.
  - Stage 4 leaves, deliberately and observably (the stage changed no behaviour; its evidence is the
    stage-4 audit record, decision 221):
    - **The craft screen is a dead end for a display proxy, and that is the row's expectation.** The
      recipe's material search reads `PlayerCamera.main.body`'s own tree and a `Physics2D` overlap that
      needs an enabled collider, while every clone render disables its collider and carries the display
      marker instead of an `ItemInstanceId` — so a recipe opened from a remote item produces no native
      inventory intent, no host round trip FOR THE REMOTE ITEM and no proxy mutation. The craft itself
      still rides the landed craft report, because what it consumes is the OPERATOR's own materials;
      the row is about the owner's item, and the owner's item is untouched.
    - **A craft opened from a remote item consumes the OPERATOR's own matching materials.** That is the
      native local UI's own rule (the material list is the local body plus nearby world items), and it
      is recorded here rather than changed: the row asks for the native local UI on the viewer, not for
      a cross-player craft.
    - **The radial weight readout still prints the operator's own encumbrance** (`PlayerCamera.cs:1901`
      reads `PlayerCamera.body`, and the clone bodies receive no encumbrance projection). A correct
      readout needs the owner's value projected onto the clone plus a readout patch; it is a display
      feature this design does not add, and the acceptance run judges whether it is acceptable.
    - **`TransferToBody` and `ApplyToLimb` keep their landed host-authoritative paths, and the owner's
      body runs no release animation for the custody move**, because the native world has no
      cross-player release call to replay — the transfer is a CUO-side custody move.
    - **The third peer's view has no automated three-client test.** It is the landed character-data
      path every other remote fact uses, with no per-viewer branch in this family, but this repository
      drives no three-client session through it, so the row stays a real-machine item.

## Related reading

- [Backlog index](../backlog/README.md) — where this work sits in the queue.
- [Remote inventory native parity rework](../backlog/review/remote-inventory-native-parity-rework.md) — the ticket this design serves.
- [Current architecture](current.md) — the kernel and authority model the host half rides.
- [Projection framework](projection-framework.md) — the display half this design leaves in place.
- [Active decisions](../decisions/active.md) — the protocol numbering policy and this cycle's decision.
