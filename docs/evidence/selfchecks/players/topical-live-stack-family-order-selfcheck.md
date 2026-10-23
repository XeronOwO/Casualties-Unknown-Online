# The live-stack family order self-check

Owner cycle: `review/topical-live-stack-family-order.md` — decision 246 applied to the family ORDER
itself. The world drag drank from a drinkable container holding a health-usable liquid only if the item
happened to be classified before the drink rule; the order, not the gesture, decided. This cycle makes
the entry an input of ONE classifier and has every one-shot routing site ask it.

## 1. Mechanism inventory

| # | Mechanism | Evidence |
|---|-----------|----------|
| 1 | The two entries' native call sites | inventory = `PlayerCamera.TryPerformRadialAction` → `Body.UseItem` / `Body.WearWearable` (`PlayerCamera.cs:1636-1651`, `Body.cs:2475-2481`); limb = `TryPerformSpecialUIAction`'s `WoundViewLimb` branch → `ApplyWoundItem` → `useLimbAction`/`ApplyToLimb` (`PlayerCamera.cs:1654-1660`, `739-763`) |
| 2 | The container both entries admit | `saline`: `usable` + `usableOnLimb`, `useAction` = `Drink(body, 100f, "drink")` (`Item.cs:1844-1874`); a health-usable liquid reaches it through `LiquidTransfer.Finish` → `Body.CombineLiquids` |
| 3 | The one order | `ItemUseFamilyClassifier.Classify(entry, …)`; the inventory entry asks wear, injection, drink, solid food; the limb entry asks wear, injection, topical, drink, solid food, limb tool |
| 4 | The host's use | `PlayerItemUseExecution.TryExecute` classifies once and dispatches; `PlayerItemUseService` keeps the endpoints and the affected side's outcome report |
| 5 | The operator's gate and measurement | `LocalUseItemEligibility.IsUseItem` / `FamilyOf(Inventory)`; `GameAdapterBridge.MeasureRemoteTopicalDose`/`MeasureRemoteDrinkDose` ask `FamilyOf(Limb)` |
| 6 | The auto-select | `CarriedItemUseTree.FindFirstUsable` asks the classifier with the entry; the Injection family is excluded (it is a named refusal) |
| 7 | The wire | unchanged: `PlayerItemUseRequestMsg.LimbIndex` identifies the entry (a named limb = the wound view, `-1` = the inventory use); `ProtocolVersion.Current` untouched |
| 8 | The affected side | unchanged: it dispatches on the result's `AppliedDose` / `DrinkDose` / `TargetEatsTheItem` / `TargetRunsLimbAction` |

## 2. Whole-family audit

| Mechanism | Verdict |
|---|---|
| World drag | a drinkable container drinks through its own `useAction`; the live stacks no longer preempt it |
| Cue | `DragUseCueTracker` asks the same `IsUseItem`, so the cue appears for the newly admitted shape |
| Wound view | its entry asks topical before drink; the dual-class container's topical half is NOT reachable for a vanilla container (its own limb action is the syringe path) — filed `todo/topical-limb-action-shape.md` |
| Host | one classifier call per request; no branch re-asks a predicate to decide a different family |
| Auto-select | the inventory entry's families only; Injection, topical and limb tool are not offered |
| Injection | asked first on both entries; the warning names the medical session; its stack-keyed scope is recorded, and the refilled-bag limit was closed by the definition-level drink classification (`done/drink-measurement-destructive-use-action.md`) |
| Medical measurement | asks `FamilyOf(Limb)`; the topical measurement still runs the item's own limb action |

## 3. Self-check table

| Mechanism × change × evidence | Case |
|---|---|
| A dual-class container is drunk by the inventory entry | `ItemUseTests.Guest_DrinksFromADualClassContainerThroughTheInventoryEntry` — red on the pre-fix tree (the host's topical branch refused it; `.acceptance/red-t1.log`: 1 failed, 1 passed), green after |
| ...and the limb entry keeps the topical family | `ItemUseTests.Use_TheDualClassContainerOnTheLimbEntry_AppliesTheTopicalHalfNotTheDrink` — the host contract (the production reachability of a vanilla dual-class container is the filed sibling) |
| The order is per entry | `ItemUseFamilyClassifierTests` (11 cases: the dual-class container, an injectable container, both kinds, a topical-only carrier, a drink-only container, a usable but not limb-drawable injectable, a limb tool, the food/limb-tool overlap, a wearable, an eats-and-replaces food, an unclaimed item) |
| The auto-select skips the named refusal | `ItemUseFamilyClassifierTests.TheAutoSelect_SkipsAnInjectableContainerTheHostWouldRefuse` |
| The world-drag gate asks no limb rule | `TopicalChainGateTests.TheWorldDragGate_DoesNotCarryTheTopicalFamily` (scans `ClassifyInventory` and the adapter's delegation) and `LimbToolChainGateTests.TheWorldDragGate_AsksNoLimbRuleAndTheMedicalGateIsWhereItIsAsked` (scans `ClassifyInventory`, the adapter's `IsUseItem`/`FamilyOf`, and the medical gate) |
| The host and the auto-select ask the ONE classifier | `TopicalChainGateTests.TheHostChainAndTheAutoSelect_AskTheOneClassifier` |
| The classifier's inventory arm carries the eat | `SolidFoodChainGateTests.TheEat_RunsOnTheAffectedSideThroughTheGamesOwnUseEntry` |

## 4. Verification design and result

- Red observed before the fix: `.acceptance/red-t1.log` — the world-drag case failed (no result:
  `序列不包含任何匹配元素` from the refused request), the host-contract case passed; focused after the
  fix `.acceptance/focused-t4.log`: 38/38.
- Build: `dotnet build CasualtiesUnknownOnline.slnx` — 0 warnings, 0 errors (`build-final1.log`).
- Normative gates: 751/751 (`.acceptance/gates-final2.log`, run after the documentation edits so
  it includes the census and checklist gates; the acceptance cycle's own run is
  `.acceptance/gates-final4.log`).
- Full behaviour suite: 6069/6069 (`full-final1.log`).
- The native half is not L0-reachable: whether a live client's drag drinks and its release on a limb
  does not is the acceptance batch's rows (both `machine`).

## 5. Limits recorded

- The wound-view topical half of the ticket's container is not reachable (its own limb action is the
  syringe path); the ticket's original acceptance sentence was corrected and the shape is filed with
  the native evidence.
- The drink measurement can run a destructive `useAction` (a refilled blood bag's `DrawBlood`) before it
  refuses; the entry split widened that shape from water to health-usable refills. Filed with the root
  fix's design space.
- Auto-select no longer offers topical, limb-tool or injection items on the inventory entry, and an
  injectable-only request is now refused by the named injection line instead of the generic "not a
  family" one; neither has a production caller in this tree.
