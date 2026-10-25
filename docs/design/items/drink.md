# Drink: drinking an item

Drinking from a container, and giving a drink to a teammate.

## What the player does

Uses a container (a bottle, a can, a bag) on themselves, or drags it onto a teammate's use circle, and expects the liquid
inside to be applied to the drinker — with its own effect, its own sound, and its own cost.

## What the game itself does

A drinkable container's use action reaches the container's own drink path, which applies the effect of each liquid stack
it holds to the body that drinks. The action is the item's own, and the amount that leaves the container is the amount
that entered the body.

## Rules and states

- **Thirst drains on the game's own curve, and a full body drains faster.** `Body.BaseThirstRate` (`Body.cs:272-278`) is
  `Time.deltaTime / (17f - tempDiff * 0.5f)` multiplied by `2f` while thirst is above 100, by `2.5f` above 175, and by the run
  setting `metabolismrate` — so the baseline is one unit per 17 s at normal temperature, a warm body drains slower, and a body
  past the scale drains two to two-and-a-half times faster.
- A container is classified by its own data, not by its name or category. The accepted record states the rule as it now
  stands: "the definition-level drink classification landed (the action is refused by name BEFORE anything runs)". A
  definition whose action never reaches the container's drink path — `liquidcentrifuge` is the record's cited case — is
  refused unrun instead of being tried out.
- The blood bags are why that order matters: "Both are `usable = true` … and their `useAction` is
  `Item.DrawBlood(item.GetComponent<WaterContainerItem>(), body.limbs[1])` … which really writes:
  `limb.body.bloodVolume -= spaceLeft / 25f`". Running such an action to find out what it does drains a patient, so an
  injectable container is not a drink and its action is never run by this entry.
- A measure that cannot reach the item's own instructions is refused rather than guessed at.
- The dose that leaves the container is the dose whose effect lands on the drinker; a disagreement between the two is the
  defect.
- **The drain is the game's own draw, split across the stacks.** `Runtime/Session/PlayerInteraction/LiquidDrainPlan.cs` is "the
  drain a cross-player limb action commits from a liquid container: the requested amount capped at the container's own total,
  split across every stack in proportion to what it holds"; it "mirrors `WaterContainerItem.CalculateDrain` — the game's own
  draw for one `Drink` / `ApplyToLimb` / `Inject` call — and holds no per-content constant: the amount comes from the caller
  (the native delegate's own dose on the operator's client, or the host's reconciliation of a reported total)".
- The drinker must be conscious: drinking is one of the two families where the TARGET itself consumes the item.
- A container with nothing to drink refuses by name.

## Refusals

| Condition | The player reads |
|---|---|
| the container has nothing to drink | `use.refused.container_nothing_to_drink` |
| the target is unconscious | `use.refused.target_unconscious` |
| the action is not a drink (the definition says otherwise) | the definition is named and nothing is run |
| the item is an injectable | `use.refused.injectable_needs_medical_view` |

## Who judges

The host validates the request and admits it. The drinker's own client runs the container's drink path on its own body,
so the effect, the sound and the container's new state are the game's own; the outcome travels back so the item's owner
sees the same container.

## What travels

The item's identity, the target, and — where the gesture measures it — the dose. The item's remaining liquid is the
owner's item state and is the same on both clients afterwards.

## What the player sees and hears

The container is raised, the game's own drinking sound plays at the drinker's position, and the drinker's state changes
by the liquid's effect. The container's fill level is what both clients then show.

## Records

Decisions 184 and 246. Accepted: `../../backlog/done/drink-measurement-destructive-use-action.md` (a non-drink action is
refused unrun), `done/cross-player-item-use-random-determinism.md`.

## Gaps

- Which vanilla liquids are drinks, and what each one does to the drinker, is the game's own table and is not yet recorded
  per liquid here.
