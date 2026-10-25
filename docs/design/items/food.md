# Food: eating an item

Eating food, and feeding a teammate.

## What the player does

Uses a food item on themselves, or drags it onto a teammate's use circle. The player expects one BITE per use — not the
whole item — with the food's own effect, the game's own eating sound, and the item's condition reduced by that bite.

## What the game itself does

A solid food's use action eats: it applies the food's own values to the body (hunger, happiness, sickness), reduces the
item's condition by the bite's own amount, plays the eating sound and produces the body's reaction. A food that consumes
itself and replaces itself with another item does so inside that same action, in terms of the body's slots.

## Rules and states

- One use is one bite: the item survives with less condition until its own condition is exhausted.
- The eaten amount, the body's effects and the sound are the food's own data; CUO does not add values of its own.
- A food that leaves a container behind cannot be handed over: the gesture is refused rather than emptying the container.
- The eater must be conscious: eating is one of the two families where the TARGET consumes the item.
- A food that replaces itself (an opened version of itself) produces a NEW item in the eater's hands; that creation is a
  registered creation, not a local object.

## Refusals

| Condition | The player reads |
|---|---|
| the food would leave something behind | `use.refused.food_leaves_something` |
| the target is unconscious | `use.refused.target_unconscious` |
| the target is dead | `use.refused.target_dead` |
| the item is not usable | `use.refused.not_this_way` |

## Who judges

The host validates and admits the request; the EATER's own client runs the game's eat on its own body and on the item it
carries, and reports what the item became. The host matches that report against the admission and hands the item's state
back to its owner.

## What travels

The item's identity and the target. The item's resulting condition travels back to its owner; the identity never changes
hands.

## What the player sees and hears

The eater chews, the game's eating sound plays, the body's reaction is the game's, and the item shows one bite missing —
on both clients.

## Records

Decisions 184 and 246. Accepted: `../../backlog/done/cross-player-item-use-random-determinism.md`. Open:
`../../backlog/todo/cross-player-eat-skips-native-steps.md` (the item must be the eater's CARRIED item, and a replacement
the food creates must be registered).

## Gaps

- The per-food values (how much a bite gives and costs) are the game's own table and are not tabulated here yet.
