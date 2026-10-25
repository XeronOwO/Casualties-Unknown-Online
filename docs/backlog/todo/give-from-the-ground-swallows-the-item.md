# Giving an item that is lying on the ground swallows it

- Status: Todo — opened 2026-10-25 from the user's reading on the deployed build: "dragging a ground item has a reaction, but the item was
  swallowed".
- Priority: High — the gesture exists natively and loses the item.
- Category: Cross-player transfer / world-item entry

## Design

`docs/design/mechanics/give-and-take.md` now records the native fact this ticket starts from: the game's own drag may start on a world item
(`PlayerCamera.TryPickupFromWorld`, `PlayerCamera.cs:1423-1441`: the press branch's first raycast is the `Item` layer and sets `dragItem`
from what it finds). The page's `Gaps` section records the hand-over of such an item as undesigned.

## What was tried and why it is reverted

`RemoteDragIntentDispatcher.CarryForGive` performed the game's own `Body.PickUpItem(dragItem, slot, false)` for a world item and then sent
the `GiveToBody` intent — the two native steps a player would perform by hand, in one gesture. On the machine the item was swallowed: the
pickup report and the give request cross the wire as two messages, and the receiving side ended with neither the item nor its row. The
method now refuses the gesture by name (one log line) and this ticket carries the finding, because a refused gesture is a known behaviour
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
