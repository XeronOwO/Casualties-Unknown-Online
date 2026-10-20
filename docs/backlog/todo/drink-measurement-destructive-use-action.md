# The drink measurement runs a destructive use action before it can refuse

- Status: Todo
- Priority: Medium-High
- Category: Item use / the drink chain's operator-side measurement
- Source: the independent adversarial review of `topical-live-stack-family-order` (2026-10-20),
  finding M1. The drink chain's own self-check already records the limit ("the item's own action has
  already run on this client"); this ticket is the root fix, and the entry split widened the shape the
  old topical-first order shielded.
- Related: `docs/backlog/done/topical-live-stack-family-order.md`,
  `docs/evidence/selfchecks/players/cross-player-native-drink-semantics-selfcheck.md`,
  `src/CasualtiesUnknownOnline.GameAdapter/RemoteDrinkUseHandler.cs`,
  `src/CasualtiesUnknownOnline.GameAdapter/Content/GameSolidFoodFacts.cs`

## The gap

`RemoteDrinkUseHandler.TryMeasure` runs the item's own `ItemInfo.useAction` and only afterwards checks
whether the delegate reached `WaterContainerItem.Drink`; a delegate that reached no drink is refused by
name, but its action has already run on the OPERATOR's client. That is the documented limit for an
item the game marks `usable` whose action is not a drink (`liquidcentrifuge` is the cited case).

The `bloodbag` and `bloodbaghuman` shape makes the limit destructive. Both are `usable = true`
(`Item.cs:1742-1747`, `1912-1913`) and their `useAction` is
`Item.DrawBlood(item.GetComponent<WaterContainerItem>(), body.limbs[1])` (`Item.cs:1762-1765`,
`1928-1931`), which really writes: `limb.body.bloodVolume -= spaceLeft / 25f` and
`wat.AddLiquid("blood", spaceLeft)` (`Item.cs:7114-7125`). The classifier's injection refusal is keyed
on the live stacks (an `injectable` liquid present), not on the definition, so a bag whose blood was
drained and refilled with a non-injectable liquid is admitted as a Drink:

- refilled with water — already exposed before this change (the injection rule never claimed it);
- refilled with a health-usable liquid (`alcohol`, `disinfectant`, …) — exposed by this change, because
  the pre-change topical-first order classified it Topical and the world drag never measured it.

The gesture is reachable in ordinary play: `LiquidTransfer.Finish` → `Body.CombineLiquids` combines any
two `WaterContainerItem`s (`LiquidTransfer.cs:36-40`, `Body.cs:1295-1298`), and the dropped-on item is
the receiving container (`PlayerCamera.cs:1602`, `Body.cs:1270-1276`). The operator then drags the bag
onto a teammate: `CrossPlayerDragUse` measures the drink, runs `DrawBlood` (the target's render clone's
`bloodVolume` drops, the operator's live bag gains blood), the capture reports no drink and the
measurement refuses — and the release has already been consumed, so the item does not drop either.

## What a fix has to decide

- Whether the consume admission classifies the definition's `useAction` delegate body once (the
  pattern `GameSolidFoodFacts` already uses: `PatchProcessor.GetOriginalInstructions`) and admits a
  drink only when the delegate reaches `WaterContainerItem.Drink`. That would retire the "already
  ran" limit for every usable-but-not-drink definition, not just the two blood bags.
- What the refusal does with a released item the gate no longer admits: the world drag falls through to
  the native drop (a usable item the game would not use), which is the same shape the topical carrier
  has.
- Whether a per-definition classification cache belongs in the adapter's content facts beside
  `GameSolidFoodFacts`/`GameConsumeFacts`, and whether mod items with a null `useAction` or an
  unreadable body are refused by name.

## Acceptance

A two-client run on the deployed artifact: the operator drains a `bloodbag` and refills it with water,
then with a health-usable liquid, and drags it onto a teammate each time. The operator's bag is
unchanged (no blood added, its stacks as refilled), the target's blood volume and the render clone's
blood volume are unchanged, and each release is refused by name (or dropped natively) without the
destructive action running. Until this lands, the widened shape stays recorded in
`topical-live-stack-family-order`'s `Limits`.
