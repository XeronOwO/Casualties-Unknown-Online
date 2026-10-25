# Giving an item that is lying on the ground swallows it

- Status: Todo — opened 2026-10-25 from the user's reading on the deployed build: "dragging a ground item has a reaction, but the item was
  swallowed".
- Priority: High — the gesture exists natively and loses the item.
- Category: Cross-player transfer / world-item entry

## Design

`docs/design/mechanics/give-and-take.md` now records the native fact this ticket starts from: the game's own drag may start on a world item
(`PlayerCamera.TryPickupFromWorld`, `PlayerCamera.cs:1423-1441`: the press branch's first raycast is the `Item` layer and sets `dragItem`
from what it finds). The page's `Gaps` section records the hand-over of such an item as undesigned.

## Implemented 2026-10-25, awaiting the machine's own verdict

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

## What a fix has to decide

The item must be the giver's before a transfer can name it, so the fix is an ordering problem, not a gesture problem: either the give is
withheld until the pickup has been ACKNOWLEDGED by the receiving side (a round trip inside one gesture, with the drag held), or the
transfer learns to name a WORLD item as its source (a protocol shape: the host moves the row from the world table straight into the
receiver's custody — the same custody change a pickup-then-give performs, in one command). The second is the smaller wire and the exact
analogue of the native release that loads a dragged world item into a container; it is the shape to design first.

## Acceptance rows

| Row | The check | State |
|---|---|---|
| 1 | A ground item dragged onto a teammate ends in that teammate's inventory (the item exists exactly once, in the receiver's custody) | not run |
| 2 | A ground item released anywhere else keeps the native behaviour (it lands where the game puts it) | not run |
| 3 | A body with no free slot answers the release by name instead of losing the item | not run |
| 4 | A ground item whose definition is hand-only follows the native hand rule | not run |

## Limits

- The one measurement this ticket has is the loss itself; the fix's shape (acknowledged pickup vs a world-item transfer command) is a design
  decision with a wire question, so it is filed rather than improvised.
