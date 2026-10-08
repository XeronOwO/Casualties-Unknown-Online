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
