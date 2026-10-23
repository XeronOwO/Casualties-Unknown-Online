# The drink measurement runs a destructive use action before it can refuse

- Status: Done 2026-10-24 — the definition-level drink classification landed (the action is refused by name
  BEFORE anything runs), and the two-client release row passed on artifact `0.1.0+6ee2f683`
  (`docs/evidence/acceptance/drink-measurement-destructive-use-action-20261024-a.md`; the refill write and the
  staged release call are declared substitutions, the OS drag was not injected).
- Priority: Medium-High
- Category: Item use / the drink chain's operator-side measurement
- Source: the independent adversarial review of `topical-live-stack-family-order` (2026-10-20),
  finding M1. The drink chain's own self-check already records the limit ("the item's own action has
  already run on this client"); this ticket is the root fix, and the entry split widened the shape the
  old topical-first order shielded.
- Related: `docs/backlog/done/topical-live-stack-family-order.md`,
  `docs/evidence/selfchecks/players/cross-player-native-drink-semantics-selfcheck.md`,
  `src/CasualtiesUnknownOnline.GameAdapter/RemoteDrinkUseHandler.cs`,
  `src/CasualtiesUnknownOnline.GameAdapter/Content/GameSolidFoodFacts.cs`,
  `src/CasualtiesUnknownOnline.GameAdapter/Content/GameDrinkActionFacts.cs`,
  `src/CasualtiesUnknownOnline.GameAdapter/Content/UseActionInstructions.cs`

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

## What landed (2026-10-24) — the definition is classified before anything runs

- **The instruction reader is shared, one copy.** `GameSolidFoodFacts`'s compiled-body reader (the delegate's
  own instructions plus one level of the GAME methods it calls) moved to
  `src/CasualtiesUnknownOnline.GameAdapter/Content/UseActionInstructions.cs`; the food classifier keeps its
  exact behaviour and now calls the shared reader, and the drink classifier uses the same one. A second copy
  of that walk was the alternative this refuses.
- **`GameDrinkActionFacts` answers three ways.** `Drinks` when the item's own `useAction` reaches
  `WaterContainerItem.Drink`; `NotADrink` when the item is absent/unusable/has no action or the action
  reaches no Drink call; `Unreadable` when the compiled body cannot be read (a dynamic method). Cached per
  item id (the body never changes while the assembly is loaded). Both non-drink verdicts fail closed.
- **The measurement refuses BEFORE running the delegate.** `RemoteDrinkUseHandler.TryMeasure` now asks the
  classifier after the use-action lookup and before the capture window opens: `NotADrink` refuses with
  `"{ItemId}'s own use action does not drink, so it was not run on this client."`, `Unreadable` with its own
  line. The old "its own action has already run" limit is retired for `NotADrink` and `Unreadable`
  definitions: a blood bag refilled with water or a health-usable liquid is refused with no `DrawBlood` on the
  operator's client. A definition classified `Drinks` whose run still skips the diverted call is refused AFTER
  it ran — see the residual in Limits.
- **A sourceless rule needs source gates, including the ORDER.** `SolidFoodChainGateTests` gained the
  drink-verdict fact (`TheDrinkVerdict_IsTheItemsOwnUseActionReadByTheGamesOwnInstructionReader`: the
  registry, the shared reader, `useAction`, the `Drink` call and `WaterContainerItem`) and an ORDER fact
  (`TheDrinkRefusal_HappensBeforeTheActionRuns`: the classifier call's source position precedes both the
  capture window opening and the `info.useAction` invocation), and its food fact was updated to pin the
  shared reader's single Harmony call site; `GameDrinkActionFacts.cs` was added to the no-id-table scan.
  Build 0/0 and the gate 12/12 green.
- **The live registry agrees (machine probe, 2026-10-24, deployed `0.1.0+6ee2f683`).** One client in a world,
  the deployed classifier asked through an in-process probe over the real `Item.GlobalItems`: `waterbottle` →
  `Drinks`, `naltrexone` → `Drinks`, `bloodbag` → `NotADrink`, `bloodbaghuman` → `NotADrink`,
  `liquidcentrifuge` → `NotADrink`; the summary over all 139 usable definitions is **33 `Drinks`, 106
  `NotADrink`, 0 `Unreadable`** — the independent review's decompiled count (33 usable liquid containers call
  `WaterContainerItem.Drink`) reproduced on the running client, so no vanilla drink is refused and the
  destructive three are refused by definition. This is the CLASSIFIER half only; the drag row below is still
  owed.
- **The measurement itself refused on the machine, with nothing run (2026-10-24, same artifact).** A water-refilled
  `bloodbag` in the local body, the deployed adapter's own `MeasureRemoteDrinkDose` (the method the wound-view
  release calls → `TryMeasure`) invoked in-process: `dose=0`, the body's blood `100 → 100`, the bag's total
  `500 → 500`, its water `500 → 500`, its blood liquid `0 → 0`, no exception — and the client log carries
  `[ItemUse] refused drink use: bloodbag's own use action does not drink, so it was not run on this client.`
  The comparison half is the decompiled defect (the pre-fix `DrawBlood` write at `Item.cs:7114-7125`) and the
  self-check's recorded limit, not a machine red: the pre-fix artifact was not re-deployed for this row, so the
  record claims only what the fixed build proves.
- **The two-client release refused, nothing written (2026-10-24, same artifact, batch `.acceptance/20261024-c/`).**
  Three clients in one world; the operator (the Steam1 member) held a `bloodbag` placed by `item-provide` and
  refilled by a probe's `DrainAll` + `AddLiquid` (water 500, then alcohol 200 — a DECLARED setup substitution for
  the native liquid transfer). The release was driven as the production branch: the host's displayed backpack
  opened through `RemoteBackpackCoordinator.Open` (the player card's own call; `opened=true`, line of sight holds)
  and then `CrossPlayerDragUse.TryUseOnDisplayed` — exactly the branch `PlayerCameraDragUsePatch` reaches on a
  centre release; the OS drag itself was not performed (the gesture's input sequence is the accepted ruling; this
  is the release branch, declared). Each run: the release consumed, the operator's log carries
  `[DragUse] refused: bloodbag could not measure a drink dose for <host>` and
  `[ItemUse] refused drink use: bloodbag's own use action does not drink, so it was not run on this client.`;
  the operator's own body `blood 100 → 100`, the bag `500 → 500` (water) and `200 → 200` (alcohol) with no blood
  liquid added; the TARGET's body (host) `blood 100` and no use line in its log (nothing reached it); and the
  operator's render clones (three bodies, one local) all `blood 100`. Both refill shapes therefore refused with
  nothing destroyed, which is the row's substance; the substitutions are the refill write and the staged release
  call, both named above and in the acceptance record.
- **What the machine still owes: nothing for the destructiveness.** The OS-level drag was not performed on this
  batch, so the record claims the production release branch and the refusal, not the pointer motion; the file
  gesture's reachability is the user ruling of 2026-10-23 that a release on the displayed backpack's centre is the
  use/wear. The acceptance record is
  `docs/evidence/acceptance/drink-measurement-destructive-use-action-20261024-a.md`.
- **Unit tests cannot reach it**: the classifier reads the game assembly's IL, which the test host does not
  load; the gate above is the source-shape half and the machine rows are the behaviour half.

## Limits this fix names

- **The capture-miss branch survives.** A definition classified `Drinks` runs the action (that is the dose
  measurement) and can still deliver no diverted call — the vanilla shape is `mindwipe`, whose delegate
  returns after `body.talker.Talk(…)` when its health gate passes, and a mindwipe-only container is the
  inventory entry's Drink (mindwipe is not injectable; reaching that container needs liquid separation).
  Such a gesture is still refused AFTER the action ran; the fix retires the limit for `NotADrink` and
  `Unreadable` definitions, not for every definition.
- **One level of calls**: a mod drink whose use action reaches `WaterContainerItem.Drink` through its own
  method (not a game method) is classified `NotADrink` and refused. That is the same one-level limit the food
  classifier records; it fails closed. A mod that installs its own `LiquidItemInfo` delegate has its
  DELEGATE's body read; through the supported item API (`ItemActionInvoker.UseAction`) a mod item is not a
  `LiquidItemInfo` at all, so it never reaches this chain — the opacity there is pre-existing and shared with
  the food classifier.
- **A dynamic delegate** (an unreadable body) is refused. No vanilla item does this; a mod that emits its use
  action at runtime keeps the cross-player drink it has today (refused, not guessed).
- **The capture is unchanged**: the classifier only reads instructions; the dose is still measured by running
  the action with its `Drink` call diverted, and the host still commits the drain.
