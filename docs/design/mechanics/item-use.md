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

**Using an item can be REFUSED by the body's happiness, and one of those refusals is a roll.**
`Body.allowUseItem` (`Body.cs:233-240`) is `totalHappiness > -20f || Random.value >= depressionChanceCurve.Evaluate(totalHappiness)`
— below minus twenty the game rolls against a depression curve, so the same gesture can fail twice in a row and succeed on the
third. `totalHappiness` is itself dragged down by bleeding when it is already under `-50f`
("`happiness - totalBleedSpeed * 15f`"), and the medical cutoff is `> -75f`. A CUO design that makes item use always available
is playing a different game.

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
- **Being ASLEEP is not a refusal condition; being unconscious from injury is** (decision 270, user ruling 2026-10-25). Every
  cross-player item use is valid on a sleeping teammate, and the operation runs the native chain on the TARGET's own client — its animation, its
  sound, its state, including any wake-on-use the game itself performs, because that is where the call runs. The distinction has to be REPORTED,
  because every other field of the health snapshot reads the same for both states: the body's own client sends `Sleeping`
  (`CharacterHealthMsg`, `Body.sleeping`), and the host's gate reads `Conscious == false && Sleeping == false` as the refusal. The refusal that
  remains names unconsciousness, not sleep.
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

**The hand case arranges the game's own truth, not a parallel one.** A feed whose item sits in the GIVER's hand (the 2026-10-25 ruling: the other player eats it out of my hand, so a full inventory must still be feedable) runs the target's own use delegate on the target's client, and a body with NO free hand slot is covered too: the standing item is then stood on the eater's own position without being parented (position and rigidbody, restored afterwards), because the game's own peel frees slot 2 itself (`Body.DropItem(2)`) before it picks the opened fruit up — left at the origin it was 497 units away and every distance check failed. That delegate resolves slots and distances against the body it is handed (`Body.SlotOf`, `Body.GetItem`, `PickUpItem`'s distance and line-of-sight check, `Item.cs:2606-2621`), so the item is parented into an EMPTY hand slot **and moved to that slot's position**: re-parenting alone left it at the world origin, the peel then dropped it there and its `PickUpItem` refused the opened fruit 497 units away, and the replacement entered the world at (0,0) with a fresh id while the owner's row was consumed (measured 2026-10-29, both clients' logs). The arrangement is a scope, not a transfer: every report it could cause is suppressed, and the custody tables are never told anything — the item never changes hands, only its state does.

**A replacement the delegate creates is the owner's row becoming another definition.** The window that catches it is armed for the item being eaten, so the object the delegate drops inside the same call can never be mistaken for the replacement; the created object gets no instance id and no world report, its definition and condition travel back in the outcome, and the eat destroys it after reading them.

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
`done/give-gesture-own-ui-and-center-use.md`. Open corrections: `../../backlog/review/cross-player-eat-skips-native-steps.md`,
`review/use-on-sleeping-target-refused.md`.

## Gaps

- The consuming families' execution must be the game's own action on the CARRIED item; where it is handed an item that is
  not the body's carried item, the slot-dependent steps of the game's action misfire — recorded in the open ticket above.
- Whether a SLEEPING (rather than injured-unconscious) teammate may be fed is not yet decided; recorded in the open ticket
  above.
- The transfer's own sound when an item is handed over is not yet reproduced; recorded in
  `../../backlog/done/give-into-backpack-plays-no-sound.md`.
