# Wear: putting a garment on a teammate

Placing a wearable on another player's body.

## What the player does

Drags a wearable onto a teammate's use circle and releases. The player expects the garment to appear on that body, on the
limb or slot the garment itself asks for, and to be gone from the giver's inventory.

## What the game itself does

The wear placement is the game's own `Body.WearWearable`: the item's own data says which limb and which slot it goes to,
and the body's own rules decide whether that place can take it.

## Rules and states

- The destination is the garment's own data (its desired limb and slot), never chosen by the gesture or by a fallback.
- The target must be alive; a garment needs no awake target, so a sleeping or unconscious teammate can be dressed.
- A place that already holds something refuses the garment rather than swapping silently.
- A garment with no place on that body is refused by name.
- The wear placement is the host's: nothing is measured on the operator's side, because a wear gesture has no dose and no
  native action to capture.
- **A worn item can act continuously, and the auto pump is the model.** `AutoPump.cs:33-41`: while worn and charged it drains
  "`DrainCharge(Time.deltaTime / 1200f)`" plus `0.002f` per stroke and raises `bloodPressure += 44f` whenever the body is below
  `85f` — a treatment with no operator and no gesture, which is exactly why a CUO rule cannot model every worn item as inert.

## Refusals

| Condition | The player reads |
|---|---|
| the garment has no place on the target | `use.refused.garment_no_place` |
| that limb or slot is not available | `use.refused.limb_or_slot_unavailable` |
| the target is dead | `use.refused.target_dead` |
| the item is not the operator's | `use.refused.not_yours` |

## Who judges

The host validates the request and places the item on the target's body; the target's client shows the garment on its own
body from the state it receives.

## What travels

The item's identity, the target and the destination the garment's data names. The item's custody changes from the giver
to the target.

## What the player sees and hears

The garment appears on the teammate's body in the right place, and the wearable's own presentation on that body is the
game's.

## Records

Decision 246 (the item's own data separates entries). Accepted:
`../../backlog/done/cross-player-item-use-random-determinism.md`.

## Gaps

- Whether a garment already worn can be replaced in one gesture is the game's own rule and is not recorded here yet.
