# Crafting: turning carried materials into an item

Making something out of what the party carries.

## What the player does

Opens the game's craft surface, picks a recipe, and makes it. The player expects the materials to be consumed from what they
carry, the new item to appear, and every other player to see the same materials gone and the same item made.

## What the game itself does

The craft is the game's own: a recipe names the materials it consumes and the item it produces, and the game removes the
materials from the crafting body's items and hands the product over.

## Rules and states

- A craft report RETIRES the materials it consumed. The accepted reading is that "every viewer applied the report's four
  `[CarriedSync] removed …` lines, the host apply line and the third peer's relay line sit in the same window, and **zero**
  `[CharSync] divergence` appears in any of the three 6 s windows" — a material the report consumed is gone from every
  client's view of that body, not only the crafter's.
- The removal is an event, not a snapshot: a viewer learns that a material left from the report that carries it.
- A recipe whose materials are not all carried is refused by the game's own rules; the refusal reaches the player on their
  own client.
- The product takes the identity of a newly created item, and that creation travels like any other.

## Refusals

| Condition | The player reads |
|---|---|
| a material is missing | the craft surface's own condition (no CUO line) |
| the recipe is unknown | the craft surface's own condition |

## Who judges

The crafter's own client performs the game's craft; the host arbitrates the report the crafter sends, and every other client
applies the same removals and the same product.

## What travels

The craft report: which materials left (per item), and the product's creation. Not the recipe's presentation.

## What the player sees and hears

The materials disappear, the item appears, and the other players' view of the crafter's inventory agrees — with the game's
own craft presentation.

## Records

Accepted: `../../backlog/done/craft-carried-fact-retirement.md` (the batch reading quoted above).

## Gaps

- The craft surface's own layout and its recipe list are the game's own UI and are not recorded here yet.
- Whether a craft can consume a material that sits inside a container on the crafter's body is not recorded here yet.
