# Item use on another player

Applying an item to a teammate: food, a drink, a wearable, a topical, a limb tool, an injection.

## What the player does

Holds the right button on an item in their own inventory, drags it, and releases it over a teammate — either inside the
teammate's use circle (the circle drawn at the centre of a displayed body view) or, for the wound view's actions, over a
limb the medical view has selected. The player expects the teammate to receive exactly what they would receive from using
the item on themselves: the item is consumed, its own effect lands on that body, the sound and animation play, and the
item's remaining state is what both clients then show.

## What the game itself does

The game runs an item's use action at two call sites, both on an item a BODY SLOT holds: the item handed to
`Body.UseItem`, and `Body.cs`'s own slot path (`this.GetItem(num).Stats.useAction(...)`). The release that mirrors it is
`PlayerCamera.TryPerformRadialAction`: at the radial centre it calls `body.WearWearable(this.dragItem)` for a wearable and
`body.UseItem(this.dragItem)` for a usable item. The actions themselves are the item's own delegates; their content is
what decides the family (see `../items/README.md`).

## The families and what each one runs

The gesture is routed to a family, and the family decides which native action runs. The dispatch order is wear,
injection, topical, drink, solid food, limb tool — one item can satisfy several, and the entry the gesture was made on
says which one was intended.

**The game's own routing is four hit targets, and one of them runs TWO actions.** `PlayerCamera.cs:1636-1670`:
`TryPerformRadialAction` fires on a hit tagged `RadialCenter` and, "if the dragged item is `wearable`" calls `WearWearable`,
then "if `usable`" calls `UseItem` — so an item that declares both flags does BOTH, in that order, from one release.
`TryPerformSpecialUIAction` is the other three targets: a `WoundViewLimb` applies the item to that limb, a `TradeThing` gives it
to the trader, and a `ContainerBack` puts it into the open container. A CUO entry has to say which of those four it is the
counterpart of; "the radial" is not one behaviour.

**A request may name an item or ask the host to pick, and the two are different.** `PlayerItemUseRequestMsg` states both:
"an item instance id of 0 means 'let the host choose the first usable consumable' (the Online UI auto button) and is always
the inventory use", and the request itself is for the direct cross-player interaction — "injectable/IV medicine is NOT a
direct one-shot request", because it goes through the medical operation the two clients perform together.

**An item's CONDITION is its dose, and every effect is proportional to what was spent.** The bandage's own action
(`Item.cs:284-300`) is a callback into the bandage minigame, and each wrap step spends `num2 = normalAngle / 12f` of condition
and applies `condition -= num2`, `skinHealAmount += num2 * 30f`, `bandageSlowAmount += num2 * 45f`, `pain -= num2 * 60f`,
`boneHealTimer -= num2 * 20f` and `dislocationTimer -= num2 * 20f` — so twelve condition units are one full wrap, each 20° step
is `1/18` of that, and a partly-used item heals exactly as much as it spends. An item's weight follows the same "condition is a
real state" rule: `Item.totalWeight` is `Stats.weight`, or `Lerp(0.1f, Stats.weight, condition)` when the definition says
`scaleWeightWithCondition` — a nearly-wrecked item still weighs a tenth of its new weight.

**A battery's charge IS the item's condition, scaled.** `BatteryItem` (`BatteryItem.cs:11-35`, `:130-150`): `hasCharge` is
`myItem.condition > 0f`, `GetCharge()` is `condition * maxCharge`, and `DrainCharge(change)` subtracts
`change / (maxCharge * 0.01f)` from the condition — so one "unit" of the drain API is one per cent of the battery's capacity, and
an item with no battery (`hasBattery` false, i.e. an empty `batteryType`) refuses every drain and reports zero charge.

| Family | The native action it runs | Where it runs |
|---|---|---|
| **Wear** | `Body.WearWearable` — the item is placed on the target body | host places, target shows |
| **Injection** | refused on this entry by name, because an injectable container's own action draws blood rather than injecting; injections belong to the medical operation | — |
| **Topical** | the item's own limb action on the wound view's selected limb | the limb's owner client |
| **Drink** | the item's use action when it reaches the container's drink path; its liquid stacks' own effects land on the drinker | the drinker's client |
| **Solid food** | the item's own use action on the eater's body and item | the eater's client |
| **Limb tool** | the item's own limb action on the limb the medical view carries | the limb's owner client |

## Rules and states

- The target's gate is the family's: a body must be alive to receive anything, and only the two families where the
  TARGET consumes the item (drink, food) additionally require a conscious target. Bandaging, splinting, injecting and a
  topical are exactly what an unconscious teammate needs, so they must not require consciousness.
- A solid food's consumption is the delegate's own: a pancake takes one bite per use and reduces its condition by its
  own amount; a closed stone fruit consumes itself, drops itself from the body and picks up an opened replacement. Both
  steps are written in terms of the body's slots.
- An item that leaves something behind (a container, a wrapper) cannot be handed over that way and is refused rather
  than silently emptied.
- A held item's condition after the use is the same on both clients; a disagreement is a defect of this mechanic.- **One roller per authoritative value.** The probe the accepted record answered was whether temporary deterministic randomness could keep both sides in step; the finding is that "the divergence does NOT reach authoritative state — every authoritative value of this family has exactly ONE roller", and the one on-screen divergence it named was fixed by deriving both machines' rolls from one state. A new random effect in this family must name its single roller.

## Refusals

| Condition | Key |
|---|---|
| one of the two players is not in the world | `use.refused.not_in_world` |
| either player has no character data yet | `use.refused.no_character_data` |
| the operator is not conscious | `use.refused.not_conscious` |
| the item is not carried by the operator | `use.refused.not_carrying` |
| nothing usable is left to apply | `use.refused.nothing_usable` |
| the food would leave something behind | `use.refused.food_leaves_something` |
| the item cannot be used through this entry | `use.refused.not_this_way` |
| the target is dead | `use.refused.target_dead` |
| the target is unconscious and the family needs it awake | `use.refused.target_unconscious` |
| the wearable has no place on the target | `use.refused.garment_no_place` |
| the named limb or slot is unavailable | `use.refused.limb_or_slot_unavailable` |
| an injectable must go through the medical view | `use.refused.injectable_needs_medical_view` |
| a topical needs the limb the wound view opened | `use.refused.topical_needs_wound_view` |
| the container has nothing to drink | `use.refused.container_nothing_to_drink` |
| a limb tool needs the limb the medical view carries | `use.refused.limb_tool_needs_medical_view` |
| the item is not the operator's to use | `use.refused.not_yours` |
| the medical view has no patient selected | `use.refused.no_patient_selected` |
| the item has no network identity yet | `use.refused.no_network_identity` |
| the item cannot be applied to a wound | `use.refused.cannot_apply_to_wound` |

Every sentence is rendered by the client that reads it, in that client's language; the refusal travels as the key, never
as prose.

## Who judges

The host validates the request (both players in world, the item really carried, the family's target gate) and admits it.
The family whose effect belongs to the target's body is then EXECUTED on that body's own client, which runs the game's
own action and reports what the item became; the host matches that report against the admission and hands the item's
state back to its owner. A cross-player use is host-validated and is never predicted by the acting client.

## What travels

The request names the item's identity, the target, and — for the wound view's actions — the limb, plus a dose where the
dose is measured. The answer carries the family's outcome for the item and, on a refusal, the key. The item's identity
never changes hands: only its state and its holder do.

## What the player sees and hears

The operator sees the item leave their hands and the target react; the target's own client runs the game's action, so the
eating, drinking or applying animation and sound are the game's own, and the game's own "in use" sound is heard by the
players near the action. A refusal reaches the operator as one line in their language plus the game's deny sound, and it
must never be a gesture that silently does nothing.

## Records

Decisions 184 (the affected side judges), 154 (authority kind), 246 (the entry decides the family), 265 (reports are
checked against data). Accepted: `../../backlog/done/drink-measurement-destructive-use-action.md`,
`done/give-gesture-own-ui-and-center-use.md`. Open corrections: `../../backlog/todo/cross-player-eat-skips-native-steps.md`,
`todo/use-on-sleeping-target-refused.md`.

## Gaps

- The consuming families' execution must be the game's own action on the CARRIED item; where it is handed an item that is
  not the body's carried item, the slot-dependent steps of the game's action misfire — recorded in the open ticket above.
- Whether a SLEEPING (rather than injured-unconscious) teammate may be fed is not yet decided; recorded in the open ticket
  above.
- The transfer's own sound when an item is handed over is not yet reproduced; recorded in
  `../../backlog/todo/give-into-backpack-plays-no-sound.md`.
