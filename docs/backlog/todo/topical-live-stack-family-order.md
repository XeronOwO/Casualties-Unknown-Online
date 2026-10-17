# A container filled with a health-usable liquid is refused by the world drag instead of drinking

- Status: Todo
- Priority: Low-Medium
- Category: Item use / family classification (the world drag versus the wound view)
- Source: the independent review of the topical-entry correction (2026-10-08), its finding m2's second
  half — the live-stack shape the game's own liquid transfer can build. Filed rather than fixed because
  resolving it is a decision about the FAMILY ORDER, not part of taking the topical family off the world
  drag.
- Related: `docs/decisions/active.md` row 246, `docs/evidence/selfchecks/players/topical-entry-and-named-limb-selfcheck.md` §5, `docs/backlog/review/mod-cross-player-native-semantics.md`

## The gap

`FamilyOf` asks the topical rule BEFORE the drink rule and reads the item's LIVE stacks
(`TopicalAdmission.IsTopicalContainer` needs a `healthUsable` stack). The game's own liquid-transfer
gesture (`LiquidTransfer.Finish` → `Body.CombineLiquids`) lets a player pour a health-usable liquid
(`disinfectant`, `alcohol`, `reliefcream`, `woundglue`, …) into a container whose item DATA is
`usable = true` — a `saline` bag, whose own inventory use is a DRINK (`useAction` → `Drink(body, 100f,
"drink")`). That container is classified Topical, so since the topical entry correction the world drag
refuses it and the release falls through to the native drop, although the item's own data says the gesture
would drink from it.

Before that correction the same container took a topical dose on the patient's most-injured limb, so the
change is an improvement in either case; what is unresolved is which of the two facts decides.

## What the reading found (2026-10-18), and why this is bigger than the gap suggests

The two limb rules and the drink rule do not disagree about the item — they answer different questions, and the
container is legitimately both:

- `InjectionAdmission.IsInjectableContainer` and `TopicalAdmission.IsTopicalContainer` each need **the item's own
  data** (`IsLimbUsableLiquidContainer(itemId)`, i.e. `usableOnLimb`) **and a LIVE stack** carrying the liquid's
  own flag (`injectable` / `healthUsable`).
- `ConsumeAdmission.IsDrinkContainer` needs only the item's own data — `usable` and its `useAction`.

So a `saline` bag holding a health-usable liquid satisfies a limb rule and the drink rule at once, and the host
chain's own comment says so out loud: *"the limb rules ask their own flag (`usableOnLimb`) while this chain asks
`usable`, so the order — not the data — is what keeps a dual-class container on the family the gesture was made
for."* The order exists because the DATA genuinely admits two answers. The missing input is not a better
predicate; it is **which gesture the player made**:

- the **world drag** drops an item on a player, and that is the cross-player form of using it from the
  inventory — `useAction`, the drink family;
- the **wound view** drops an item on a LIMB, and that is `useLimbAction` — the topical or injection family.

`LocalUseItemEligibility.FamilyOf` is asked by all three callers and cannot tell them apart, so it cannot answer
this, and the ticket's third bullet is the one that decides the shape: **FamilyOf has to take the entry as an
input.**

**That is why the first bullet's fix is not local.** The world-drag GATE could classify by the item's data on
its own, but the request it sends reaches the HOST CHAIN, which asks the same `FamilyOf` and would still dispatch
the topical branch — so the gate would admit a drag the host then handles as something else, which is exactly the
divergence the shared predicate exists to prevent. Changing the verdict and keeping the callers in step means the
gesture (or the family it implies) has to reach the host's decision — a wire field, or a decision taken on the
entry the request already identifies.

**Consequence for this ticket's priority**: it is filed `Low-Medium` as a family-order question, and the reading
says it is a cross-layer one: the predicate's signature, its three callers, and whatever the request carries to
the host. That is worth knowing before someone starts on the first bullet alone.

## What a fix has to decide

- Whether the world drag classifies by the item's own DATA (its `usable` flag and `useAction` → the drink
  family) rather than by the live stacks' topical admissibility. That is the isolation rule's own answer —
  "the item's own data says which action each entry runs" — and it would leave the container drinkable from
  the world drag and still topically applicable from the wound view.
- What the wound view does with the same container: its stacks ARE health-usable, so its topical half is
  legitimate there. Two entries then run two different actions on one item, each by its own fact.
- Whether `FamilyOf` keeps one order for all three of its callers (the host chain, the world-drag gate and
  the medical view's dose measurement) or takes the entry as an input, which is the shape that would let
  each call site apply its own rule without an ordering argument.

## Acceptance

A two-client run on the deployed artifact: a `saline` bag holding a health-usable liquid, dragged onto a
teammate in the world, drinks from the bag through the drink family's own path instead of being refused;
the same bag dragged onto a limb in the wound view applies its topical half to that limb. Until this lands
the world drag refuses such a container, which the topical self-check's limits record rather than hide.
