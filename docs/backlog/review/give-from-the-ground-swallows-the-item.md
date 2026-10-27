# Giving an item that is lying on the ground swallows it

- Status: Review — the ground path is exercised end to end on the deployed build (2026-10-26 below); opened 2026-10-25 from the user's reading on the deployed build: "dragging a ground item has a reaction, but the item was
  swallowed".
- Priority: High — the gesture exists natively and loses the item.
- Category: Cross-player transfer / world-item entry

## Design

`docs/design/mechanics/give-and-take.md` now records the native fact this ticket starts from: the game's own drag may start on a world item
(`PlayerCamera.TryPickupFromWorld`, `PlayerCamera.cs:1423-1441`: the press branch's first raycast is the `Item` layer and sets `dragItem`
from what it finds). The page's `Gaps` section records the hand-over of such an item as undesigned.

## Implemented 2026-10-25, then REVERTED — see the ruling below the code history

The refusal is gone; the gesture now runs the two native steps with the race REMOVED rather than tolerated:

1. `RemoteDragIntentDispatcher.TryCarryIntoBody` performs the game's own `Body.PickUpItem(item, slot, force: false)` into the first slot that
   accepts it and verifies the slot by reading it back. A body with no room refuses the give by name (the player makes room first) — `force: true`
   would put an item where the game itself would not.
2. The `GiveToBody` intent is NOT sent in the same breath. It is parked in `_pendingGive` and `Tick()` (called once per frame from
   `GameAdapter.Update` through `GameAdapterBridge.TickGiveQueue`) sends it only when `IItemControl.OwnsCarriedItem` says this body's ROW carries the
   item — the row the pickup report produces. A row that does not arrive inside 3 s drops the intent with one line and leaves the item where the
   pickup put it, in this body.

That is the fix for the measured swallow: the old path sent the give while the row still said "world item", so the host and the receiver disagreed
about an item nobody's row owned.

**Not yet exercised end to end.** The in-process `give-probe` picks its item from the BODY's children, so it cannot hand the dispatcher a world item;
the probe run after this change exercised the CARRIED path again (`[RemoteIntent] GiveToBody captured for item 5197989525`), not the new one. The
ground path needs a real drag of a ground item onto the teammate, which is the user's next test — logged lines to look for:
`carrying the ground item … into this body`, then `the carried row for … arrived — sending the give it was waiting for`.

## What was tried and why it is reverted (the FIRST attempt, superseded above)

`RemoteDragIntentDispatcher.CarryForGive` performed the game's own `Body.PickUpItem(dragItem, slot, false)` for a world item and then sent
the `GiveToBody` intent — the two native steps a player would perform by hand, in one gesture. On the machine the item was swallowed: the
pickup report and the give request cross the wire as two messages, and the receiving side ended with neither the item nor its row. The
method then refused the gesture by name (one log line) and this ticket carried the finding, because a refused gesture is a known behaviour
while a lost item is not.

## REVERTED 2026-10-25 — the user's ruling kills the whole approach (read this first)

The pickup-then-give implementation above was **wrong by design**, and the machine said so before the user did: the items ended up in the giver's
backpack "in completely wrong positions". The user's ruling, recorded on `../../design/mechanics/give-and-take.md`:

> A drag has an explicit DESTINATION. "Find a free slot" belongs in exactly one place — the give being placed into the receiving player's backpack —
> and nowhere else. Putting the item into an arbitrary slot of the giver's body first is not a step of the gesture; it is an invented intermediate
> state the player never chose.

So the intermediate pickup rung is reverted and the gesture is refused by name again (one line, naming the ticket) until the hand-over is ONE move
from the world to the aimed destination. The host log that decided it:

```
21:36:00.975 carrying the ground item 9492956821 into this body for the give
21:36:00.975 picked the ground item 9492956821 up for the give; the intent waits …
21:36:00.992 the carried row for 9492956821 arrived — sending the give it was waiting for
21:36:00.992 GiveToBody captured for item 9492956821 of 76561198246659 (slot 1, body 76561198863287957)
21:36:00.992 [WRN] refused GiveToBody: 76561198246659 has no item instance 9492956821.
```

Two independent reasons it could never have been right: the item reached a slot nobody aimed at (the ruling), and the give was then refused because
the ownership check reads the sender's **character snapshot**, not the kernel row the waiter read — the snapshot is a report and lags.

## The shape to build next (read 2026-10-25, no code yet)

The one-move hand-over already has its command: **`PickUpItemCommand`** (`src/CasualtiesUnknownOnline.GameState/Domains/Items/PickUpItemCommand.cs`)
is declared as "An item moves from World/Contained into the actor's carried inventory" and it carries a **`NewOwner`** beside the actor — so the host
can move a WORLD row straight into the TARGET's custody with the giver as the actor. That is the same custody change the two-step performed, in one
command, and it needs no new wire message.

**And the target already holds the item.** World items are synced to every client, so the receiving player's own copy of the ground item exists on
their machine. That makes the native chain available verbatim: the TARGET runs the game's own pickup on its own object — the same "the affected
player's client runs the native call" rule as decision 270 — and no item payload crosses the wire at all. The intent kind for it already exists
(`RemoteInventoryIntentKind.PickUpToSlot = 6`), and the target's applier already implements it (`RemoteIntentApplier.ApplyPickUpToSlot` →
`RemoteIntentSlotRelease.Plan` + `body.PickUpItem`).

What the change touches, in order:

1. **The host's give branch** (`PlayerRemoteInventoryIntentService`): the `TryFindItem(source.Items, msg.ItemInstanceId)` refusal is right for a
   carried item and wrong for a world item. When the sender does not carry it AND the kernel row is `World`, the host forwards `PickUpToSlot` to
   `msg.TargetBodySteamId` with `TargetSlotIndex` — the destination the player aimed at — instead of refusing or running `BeginGive`.
2. **The applier's lookup** is the one real gap: `RemoteIntentApplier` resolves every item with `CarriedItemLocator.FindById(body, id)`, i.e. items in
   the owner's body, so a world item needs a locator by instance id over the scene's world items (the same registry `ItemWorldSync` already walks).
3. **Everything after that is already the game's**: the target's `body.PickUpItem` runs the native guards, sound and puff, its own pickup report
   commits `PickUpItemCommand` on the host, the row leaves `World`, and the giver's world object is retired by the reconcile's kill sweep — nothing is
   destroyed by hand and no intermediate slot exists.
4. **The giver's client** stops refusing: `RemoteDragIntentDispatcher.CarryForGive` returns true for a world item with an authoritative id, and the
   intent it sends is the `GiveToBody` the release already produced (the host decides the native call).

Rows 1-4 below then hold without any invented intermediate state: the item goes from the ground to the aimed place, and the only free-slot search in
the whole path is the receiver's placement.

## What a fix has to decide (superseded by the shape above)

The item must be the giver's before a transfer can name it, so the fix is an ordering problem, not a gesture problem: either the give is
withheld until the pickup has been ACKNOWLEDGED by the receiving side (a round trip inside one gesture, with the drag held), or the
transfer learns to name a WORLD item as its source (a protocol shape: the host moves the row from the world table straight into the
receiver's custody — the same custody change a pickup-then-give performs, in one command). The second is the smaller wire and the exact
analogue of the native release that loads a dragged world item into a container; it is the shape to design first.

## Built 2026-10-25 (`.acceptance/batch-groundgive2`): the receiver's own native pickup

The three steps above are implemented and deployed:

1. `PlayerRemoteInventoryIntentService` — when the sender does not carry the named item AND the kernel row is `World`
   (`PlayerCustodyRows.IsInWorld` via `PlayerInventoryTakeService.IsWorldItem` → `ItemKernelAuthority.IsWorldItem`), the give becomes
   `ForwardWorldPickupToDestination`: a `PickUpToSlot` aimed at `TargetBodySteamId` with the slot the release named, sent with `Forward` so the
   host-as-receiver case runs the applier locally. The old refusal is gone from that path.
2. `RemoteIntentApplier` resolves a `PickUpToSlot` item that is not in the body through `ItemApplication.FindWorldItem` (the same scene lookup the
   world-row applier uses) and `ApplyPickUpToSlot` handles the world case by calling `body.PickUpItem(item, targetSlot, false)` and verifying the
   slot — NO slot-release plan runs, so no occupant is dropped for a destination the player did not choose. A destination that refuses the item is
   answered by name.
3. `RemoteDragIntentDispatcher.CarryForGive` no longer refuses a world item: the gesture names it and the host decides the native call.

What is NOT claimed: no machine run has exercised it yet. The batch that follows this deploy is judged by three log lines — the host's
`ground hand-over: sent PickUpToSlot for item …`, the receiver's `PickUpToSlot resolved item … from the WORLD`, and the receiver's pickup report — and
by the item existing exactly once, in the receiver's inventory, with the giver's world object gone.

## Acceptance rows

| Row | The check | State |
|---|---|---|
| 1 | A ground item dragged onto a teammate ends in that teammate's inventory (the item exists exactly once, in the receiver's custody) | **PASS 2026-10-27** (deployed): `give-probe mode=give-world` handed a world plastic chunk to the guest, whose own body census then listed `slot0=plasticchunk#1421955155907` and whose row reads `Carried`, owner = the guest — one instance, in the receiver's custody |
| 2 | A ground item released anywhere else keeps the native behaviour (it lands where the game puts it) | not run |
| 3 | A body with no free slot keeps both items (user ruling 2026-10-27: the aimed slot's occupant drops into the world, the native slot-release rule) | **PASS 2026-10-27** (deployed): the guest's six slots were all full and the aimed slot 0 held a lantern; the give landed the chunk in slot 0 and the lantern's row became `World` — both items exist, neither is lost. The earlier "refuse by name" wording was the design's guess and the user replaced it with the native rule |
| 4 | A ground item whose definition is hand-only follows the native hand rule | not run |

## Limits

- The one measurement this ticket has is the loss itself; the fix's shape (acknowledged pickup vs a world-item transfer command) is a design
  decision with a wire question, so it is filed rather than improvised.

## The ground path exercised end to end (2026-10-26 21:12, deployed `8cfd5501`)

The gesture could not be driven before because `give-probe` picks its item from the BODY's children. It now has a third mode, `give-world`, which hands
the same release seam (`ReportLocalReleaseOntoProxy`) a real WORLD object — the drag's own entry, without the cursor. On the guest:

```
21:12:01.696 [RemoteIntent] GiveToBody captured for item 43852695189 of 76561198863287957 (container 0, slot 1, body 76561198281246659)
21:12:01.737 ([host]) PickUpToSlot resolved item 43852695189 from the WORLD (the giver's hand-over); the local body picks it up
21:12:01.744 ([host]) [ItemTrace] op=18 item=43852695189 origin=OnItemPickedUp result=Committed(2) events=[Pickup]
21:12:01.745 ([host]) replayed native PickUpToSlot on item 43852695189 — a drop report is pending and announces it on the next
21:12:01.745 ([host]) ground hand-over: sent PickUpToSlot for item 43852695189 to 76561198863287957
```

and the outcome on the receiver, read back by id: the host's body census lists `instanceId: 43852695189` in **slot 1**, the slot the release named. So the
item went from the ground into the aimed slot of the receiver by the receiver's own native pickup — no intermediate slot in the giver, nothing
destroyed by hand — and the custody projection stayed at `mismatches=0`.

**Judged**: the swallow is gone on the deployed build and the item lands where the player aimed. **Owed**: the give's own acceptance rows on the
ticket (a full body refusing by name, a container destination) and a frame with a hand on the mouse.

## The full-body row, and a second ground object that was refused (2026-10-26 21:14)

**The full-body case did not happen**: `item-provide` on the host answered `no-free-slot` ("every body slot is occupied or unusable"), yet the ground
hand-over of a fresh lantern then succeeded and the host's census grew 13 → 14 items with nothing displaced. So the body the recipe calls full still
accepts the destination the release names — the refusal row needs a receiver that is full in the NATIVE sense (every slot the pickup guard tests),
which the recipe's own "usable slot" search does not produce. Owed.

**A second ground object was refused by name**, and the shape is worth recording: a dogfood the guest had DROPPED (`item-provide mode=drop`) was
refused twice with `refused GiveToBody: … has no item instance 14451443822` — the carried-item branch — while a lantern that was created straight into
the world went through. That is correct behaviour for an object whose kernel row is not `World`, but it means the probe had picked a WORLD OBJECT whose
row does not say world: either the drop report had not committed yet (it was retried four seconds later, same answer) or that object is another instance
of the ghost family (`todo/a-world-item-is-rendered-by-a-display-object.md`). **Root cause of that refusal, found the same hour**: the probe had picked a DISPLAY object — a standing materialization of a member's carried row — because
its filters covered `RemoteCloneRender`, containers and bodies but not `StandingItems.Is`. With that marker also excluded (reached by name, like every
other adapter type in this recipe) the same dropped dogfood went through: `PickUpToSlot resolved item 13787924117 from the WORLD … ground hand-over
sent`. So the refusal was the probe's pick, not the product's rule, and no row-kind read is needed.