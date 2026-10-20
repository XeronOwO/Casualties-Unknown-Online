# A health-usable liquid cannot be applied through the wound view when its container's own limb action is not a topical one

- Status: Todo
- Priority: Medium
- Category: Item use / topical admission (the wound view's limb action shape)
- Source: the independent adversarial review of `topical-live-stack-family-order` (2026-10-20),
  finding B1. Its acceptance sentence — "the same bag dragged onto a limb in the wound view applies its
  topical half" — was checked against the native call site and does not hold; this ticket records the
  shape and the two branches the current predicate models wrongly.
- Related: `docs/decisions/active.md` row 246,
  `docs/backlog/done/topical-live-stack-family-order.md`,
  `src/CasualtiesUnknownOnline.GameAdapter/RemoteTopicalUseHandler.cs`,
  `src/CasualtiesUnknownOnline.Runtime/Session/PlayerInteraction/TopicalAdmission.cs`

## The gap

`TopicalAdmission` admits a container when the item's own data says `usableOnLimb` AND a live stack is
`healthUsable`. It treats `usableOnLimb` as "its limb action is a topical application", and that is not
what the flag means. Native `PlayerCamera.ApplyWoundItem` (`PlayerCamera.cs:739-763`) is:

- `usableOnLimb` set → `item.Stats.useLimbAction(this.selectedLimb, item)`, whatever method that
  delegate calls;
- `usableOnLimb` clear → `waterContainerItem.ApplyToLimb(this.selectedLimb, 100f)`, which is the
  native topical branch for a container.

Two reachable shapes are therefore handled wrongly:

- **A drinkable container refilled with a health-usable liquid.** `saline` and `ringersolution` are
  `usable` + `usableOnLimb`, and their own `useLimbAction` is an injection: `StartMinigame(new
  SyringeMinigame(delegate(float mult) { wat.Inject(limb, mult * 80f); } …)` (`Item.cs:1859-1866`,
  `1891-1898`); `bloodbag`/`bloodbaghuman` call `Inject(limb, 375f)` directly (`Item.cs:1756-1761`,
  `1922-1927`). `TopicalAdmission` admits such a bag as soon as a health-usable liquid is poured in,
  so the remote medical view routes the release to `RemoteTopicalUseHandler.TryMeasure`, which runs the
  item's own limb action, captures no `ApplyToLimb`, refuses ("its own native limb action applied
  nothing to the treated limb") — after the syringe delegate has already been started. The native
  action for this container is the injection path, not a topical application.
- **A container the native topical branch is actually for.** A `waterbottle` is `usableOnLimb = false`
  (`Item.cs:3155-3175`); with a health-usable liquid poured in, `ActuallyUsableOnLimb` is true (the
  live stacks) and `ApplyWoundItem` reaches `WaterContainerItem.ApplyToLimb(limb, 100f)`. CUO's
  `TopicalAdmission` requires `usableOnLimb`, so the medical gate (`IsMedicalLimbUseItem`) refuses the
  shape the game's own wound view would apply topically.

The predicate's evidence is the item's flag plus the live stacks; the fact it needs is which method the
item's own limb-action delegate calls. The solid-food family already answers that kind of question by
reading the delegate's compiled body (`GameSolidFoodFacts`, `PatchProcessor.GetOriginalInstructions`),
so the mechanism exists in this tree.

## What a fix has to decide

- Whether the topical family's admission asks the item's own limb action (the delegate's compiled
  body: does it reach `WaterContainerItem.ApplyToLimb`?) instead of `usableOnLimb`, and what happens to
  a delegate that is neither a direct `ApplyToLimb` nor a direct `Inject` (the syringe minigame).
- What a syringe container holding only a health-usable liquid does on the wound view. The native
  action injects and applies nothing (the liquid is not `injectable`); the injection chain's
  `InjectionAdmission` requires an injectable stack, so it refuses to claim the item. Running the
  native injection path faithfully, refusing it by name, and applying it topically are three different
  behaviours, and the choice is the user's.
- Whether the `usableOnLimb == false` + health-usable shape joins the topical family (the native
  `ApplyToLimb` branch) and how its measurement works (the item has no `useLimbAction` at all, so the
  dose is the native call's own `100f` literal, not a measured delegate).
- Whether the medical gate (`IsMedicalLimbUseItem`) and `RemoteMedicalOperationHandler.TryDispatchLimbUse`
  keep their own order or follow the same classification.

## Acceptance

A two-client run on the deployed artifact: a `saline` bag holding a health-usable liquid released on a
teammate's limb through the remote wound view behaves as the design decides (no started syringe
minigame, a named outcome); a `waterbottle` holding a health-usable liquid released the same way
applies it topically to the named limb when that is the decided behaviour. Until the design is chosen,
the shape stays refused as recorded in `topical-live-stack-family-order`'s `Limits`.
