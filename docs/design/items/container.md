# Container: items that hold items

Backpacks, pouches and bags — the items whose contents are other items.

## What the player does

Puts an item into a container, takes one out, and looks inside. In co-op the container may belong to another player, and
the player expects its contents to be live and its capacity to be the game's own.

## What the game itself does

A container holds items through the game's own container path: it loads an item when the item fits, unloads one when the
player takes it, and answers weight and space questions for the items offered to it. A backpack is an item that is also a
container, so it can be carried, dropped, given and looked inside while it holds things.

## Rules and states

- A container's contents are not a body slot: an action that reads a body's slot cannot see an item that sits inside the
  body's pack, and an action that must run on a carried item must not be handed a container's child.
- Capacity rules (weight, kind, space) are the container's own; an item that does not fit is refused rather than silently
  placed elsewhere.
- **The fit test is the game's own, applied twice — once to choose, once to accept.**
  `src/CasualtiesUnknownOnline.GameAdapter/Items/ItemContainerFit.cs` reads "the definition's `Container` component and applies
  the SAME rule the receiving client's native `Container.CanHoldItem` applies when the item arrives —
  `item.totalWeight <= maxWeightPerItem`, `holdingWeight + item.totalWeight <= maxWeight`, and the tag restriction when the
  container declares one". It also mirrors the adjustment `Container.Awake` makes to both limits
  (`reversing/Assembly-CSharp/Assembly-CSharp/Container.cs:20-32`, where `maxWeight += 0.001f` and
  `maxWeightPerItem += 0.001f`) "rather than under-reporting a live container's room
  by one gr[am]", and takes the item's weight from `Item.totalWeight`'s own formula ("the definition weight, scaled by
  condition when the definition says so", `reversing/Assembly-CSharp/Assembly-CSharp/Item.cs:13-23`) — which is `Mathf.Lerp(0.1f, Stats.weight, condition)` when
  `Stats.scaleWeightWithCondition`, so a nearly-wrecked item weighs a tenth of its new weight. A definition this build cannot
  read fails the fit rather than guessing.
- The contents travel with the container: dropping, giving or storing a container moves what is inside it.
- A container with something inside cannot be handed over as "just the container"; the design decides per item whether it
  may be emptied first or refused.
- A view-only pack accepts no change in either direction.- **A child that moves keeps its container fact.** A child moved out of a container into a slot must not lose the fact that it came from one: the accepted reading for the release onto an occupied and an empty ring slot is "zero `[CharSync] divergence` on every client, the bag reading one child fewer on the owner's tree, the host's kernel record and the third client's proxy tree, with the child in the destination slot".- **Contents never appear as world drops.** A carried container's contents must not periodically surface as drops on another client's view: the defect was named by the ticket that closed it ("Guest carried container contents periodically appear as world drops on the host view"), and its container scenario read rows 1–… pass with the panel frames.
- **Two departures in one frame both commit.** A pending report is never overwritten by a same-frame second one: the accepted reading is "its two departures register out of ONE frame and BOTH commit, BOTH children reach the host's world table" with no `InvalidTransition` in the session.
- **A container move is reported as an event, not left to a snapshot.** The accepted record's shape is "the owner's client reporting the TARGET root's contents fact once per child and both viewers at zero divergence" — a viewer learns of a move from the event that carries it, because a snapshot alone would leave a stale child on one client.
- **Auto-pickup falls through from the hands to a surface container, and a wearable is WORN.** `Body.AutoPickUpItem`
  (`Body.cs:494-530`) refuses a `noautopickup` item, then puts a non-wearable into the first empty slot or, failing that, into
  the first surface container whose own `CanHoldItem` accepts it; a wearable replaces what occupies its slot by DROPPING the old
  item (`GetWearableBySlotID` → `DropItem` → `WearWearable` → `UpdateWearables`). So a full hand falls through to a bag rather
  than the item being lost.
- **The inventory is a TREE and the game walks it.** `GetAllItemsThorough` (`Body.cs:530-560`) recurses into each held item's
  `Container`, which is what `FindByIdThorough` and every "does this body have X" rule rely on.

## Refusals

| Condition | The player reads |
|---|---|
| the container cannot hold the item | a line naming the container and the reason |
| the container is view-only | the backpack notice's gesture refusal |
| the item would leave a container behind when handed over | `use.refused.food_leaves_something` (the food case; other kinds are refused by their own rule) |

## Who judges

The container's contents are the holder's state: the host owns the fact, the holder's client writes its own side of a
change, and the host arbitrates a conflict.

## What travels

The container's identity, its contents (their identities and states), and the holder.

## What the player sees and hears

Opening a container shows what is inside with the game's own layout; an item placed or taken plays the game's own
handling sound and the container refreshes.

## Records

Decisions 155, 156 and 161. Human detail: `../model/item-custody.md`. Accepted:
`../../backlog/done/remove-legacy-view-items-remote-inventory-detail.md`,
`done/remote-inventory-native-parity-rework.md`, `done/tab-backpack-open-close-immediately.md`.

## Gaps

- A container nested inside another container has no dedicated record; its custody and its view are the open question.
