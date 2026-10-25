# Topical: applying a medicine to a limb

Applying a dose of a medicine to another player's wound.

## What the player does

Opens a patient's medical view, selects the wounded limb, drags a medicine onto it and releases. The player expects the
dose to be applied to that limb — the burn treated, the infection fought, the bleeding stopped — with the game's own
application and its own cost.

## What the game itself does

The topical's own limb action applies a measured dose to the limb the wound view selected: it is the game's
`ApplyToLimb` path of the medicine, run against the patient's limb, and it writes the limb's own state (the treated
condition) rather than the body's.

## Rules and states

- The limb is the one the wound view carries: a topical that arrives without that limb is refused rather than applied to
  a guess, which is why the wound view's release is the only entry for it.
- The dose is measured on the operator's side and travels with the request; the patient's side applies exactly that dose.
- The patient must be alive; no consciousness is required, because a topical is applied BY the operator.
- A medicine whose action is not a limb action is not a topical, and it is classified by its own data.

## Refusals

| Condition | The player reads |
|---|---|
| the topical arrived without the wound view's limb | `use.refused.topical_needs_wound_view` |
| the named limb is unavailable | `use.refused.limb_or_slot_unavailable` |
| the item cannot be applied to a wound | `use.refused.cannot_apply_to_wound` |
| the target is dead | `use.refused.target_dead` |

## Who judges

The host admits the request with its dose and limb; the patient's client runs the medicine's own limb action on its own
limb and reports the resulting item and limb state.

## What travels

The item's identity, the patient, the selected limb and the measured dose. The item's remaining content and the limb's
state travel back so both clients agree.

## What the player sees and hears

The medicine is applied with the game's own animation and sound; the patient's limb shows the treatment, and the item's
remaining dose is what both clients then show.

## Records

Decisions 184 (the affected side judges), 246 (the entry decides the family). Open:
`../../backlog/review/topical-limb-action-shape.md` records the shape this entry must keep.

## Gaps

- The per-medicine dose and its effect on each wound kind are the game's own table and are not tabulated here yet.
