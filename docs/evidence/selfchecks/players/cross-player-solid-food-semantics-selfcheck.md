# Cross-player solid food semantics from the game's own data self-check

Ticket: `docs/backlog/todo/mod-cross-player-solid-food-semantics.md` (steps 1 and 2 landed
earlier in the same branch; this cycle is step 3, the food chain itself). Rule of the family
(`mod-cross-player-native-semantics` Parts A and B): the game's own code runs the effect on the
affected side, the host keeps admission, the resource and the arbitration, and no CUO table
answers a content question the game can answer itself.

## 1. Mechanism inventory

| # | Mechanism | Evidence |
|---|---|---|
| 1 | The native use dispatch | `Body.UseItem(Item)` = `if (item.Stats.usable) item.Stats.useAction(this, item)`, called from `PlayerCamera`'s radial-centre release (`reversing/Assembly-CSharp/Assembly-CSharp/Body.cs:2475-2481`, `PlayerCamera.cs:1646`) |
| 2 | The eating body methods | `Body.Eat(float hungerAmount, float weightGain)` clamps hunger at 125, converts the overflow into `sicknessAmount` and rolls vomit/burp/dirty-hands (`Body.cs:2259-2294`); `Body.Drink(float amt)` (`:3680-3685`). Both are called from inside the food delegates, so the clamps and the rolls are the game's own and happen on the body that ate |
| 3 | No food data exists | `ItemInfo` has no nutrition member (`ItemInfo.cs:87-185`); its `category` string is a DISPLAY classifier — `PlayerCamera.ItemHoverDescription` reads it for the unidentified-item label (`PlayerCamera.cs:1046`) and `TraderScript` sorts by it (`:681`). Measured over `Item.cs`'s table: 25 ids carry `"food"`, 18 edible ones are filed under `"custom"` (`geofruit`, `browncap`, `popfruit`, `mushpear`, `cactusflesh`, `foliage`, …), and `ketchup` is `"food"` although its action is a `WaterContainerItem.Drink` (a drink) |
| 4 | The shape flags do not separate the family | Measured over the same table's 139 use actions (a session measurement over that file; the named delegates are individually checkable in it): "usable, not left-click-triggered, not a liquid container, not wearable" still admits 24 items that act on the USER's own world — a watch whose action calls `item.GetComponent<Talker>().Talk(...)` (`Item.cs:5547`), a geiger counter that flips its own click loop (`:5597`), dynamite that arms `CustomItemBehaviour.Invoke("DynamiteExplode", 5f)` (`:6671`), a drain that writes `FluidManager.main.fluid[...]` where the eater stands (`:1653`), a present that spawns a hat and a plushie (`:6695`) |
| 5 | The delegate is the only authority | Of the 139 use actions, 41 call `Body.Eat`/`Body.Drink` directly and one more (`nondescriptcan`) does so one call away through `NonDescriptCan.Eat(Body)` (`NonDescriptCan.cs:67-83`) — 42 in all. The ticket's own census (41 + the component-driven can) states the same set |
| 6 | The two item-shapes | `exposedcore`'s action destroys the item object itself (`Item.cs:3578-3590`) → the item is gone whatever its condition says; `bucketofchicken` and `popcorn` instantiate a replacement and hand it over with `body.PickUpItem(..., body.SlotOf(item), false)` (`:2790-2803`, `:2834-2847`). `Body.SlotOf` returns **0** for an item that is not held (`:1333-1343`) and `Body.DropItem(Item)` is a no-op for it (`:1441-1451`), so on a standing object the swap's pick-up runs the native `DoPickupCheck` — distance to the parked object from the eater — and pops the game's own "too far" alert while leaving the instantiated object behind |
| 7 | The item object the eat needs | step 2's standing object: the local, inert, id-addressable incarnation of a carried row, carrying every switch of the recipe (ticket *The materialize path*) |
| 8 | What was deleted | `RemoteConsumeCatalog` (the 25-row `Food` table and the type), `RemoteConsumeApplication` (`ApplyFood`), `RemoteFoodEffect`. Their consumers were `PlayerItemUseService`'s food arm, `LocalUseItemEligibility.IsUseItem` and `CarriedItemUseTree.IsActuallyUsable` |
| 9 | What replaced it | Runtime: the `ISolidFoodSemantics` seam + `NoSolidFoodSemantics`, `SolidFoodAdmission` (the one rule), `SolidFoodVerdict` (the three shapes), `SolidFoodEatGrants` + `PlayerSolidFoodEatService` (the admission and the outcome half), `PlayerItemUseCommit` (where a use's item state lands), `PlayerItemEatOutcomeMsg` + its handler (NetMsg 143), `PlayerItemUseResultMsg.TargetEatsTheItem` with its journal/wire twins. Adapter: `Content/GameSolidFoodFacts` (the verdict), `NativeSolidFoodEat` (the eat on the affected side), `ItemUseSync`'s standing branch (the report), `PlayerInteractionApply`'s handler (the trigger) |
| 10 | Why the cross-owner write needs the admission | the item domain refuses a member's report about another member's carried item — `KernelCommandGateway.MayReportDestroyed` answers `Ignore` for a carried item the sender does not own, pinned by the destroy-authority case; the host's admitted eat IS the authorization for the one outcome report that follows |

## 2. Whole-family audit

The family is every site that could decide, run or land this use, and all of them moved in one pass:

- the operator's gesture verdict (`LocalUseItemEligibility.FamilyOf` / `IsUseItem`) — the catalog's id
  check replaced by the seam, in the host chain's own family order;
- the host's auto-select and family dispatch (`CarriedItemUseTree.FindFirstUsable` /
  `IsActuallyUsable`, `PlayerItemUseService`'s chain) — the tree gate and the chain both ask the one
  admission rule, so auto-select cannot offer an item the chain refuses;
- the request half (the new `TargetEatsTheItem` result) — the host commits nothing, which is the
  difference from every earlier family and is pinned by a case;
- the affected side's run (`NativeSolidFoodEat`), whose trigger is the ordinary result handler;
- the item-level report (`ItemUseSync.OnItemUsed`) — the standing object's own route, and the
  `IPlayerInteractionControl` surface it calls;
- the host's outcome handling and the commit (`PlayerSolidFoodEatService`, `PlayerItemUseCommit`),
  which every other family's item commit now also runs through, so the eat cannot drift from them;
- the content gates: the deleted table's census row in the wear chain's gate (the directory's
  remaining catalogs are an exact set) and the new solid-food chain gate.

## 3. Self-check table

| Mechanism | Change | Evidence |
|---|---|---|
| The family's verdict | the item's own use action is read through `Item.GlobalItems` and the delegate's instructions | `SolidFoodChainGateTests.TheFamilyVerdict_IsTheItemsOwnUseActionReadByTheGamesOwnInstructionReader` (syntax-level: `Item.GlobalItems`, `PatchProcessor.GetOriginalInstructions`, `useAction`, and the seven native call names) |
| The admission's two questions | `Classify` (the family) and `IsFeedable` (what the path carries) | `SolidFoodAdmissionTests.TheFamily_IsTheItemsOwnUseActionFeedingABody`, `AUseActionThatDoesNotFeedABody_IsNotThisFamily`, `AFoodThatHandsTheEaterAReplacementObject_IsRefusedNotCarried`, `AFoodWhoseUseDestroysTheItem_IsCarriedAndItsOwnerLosesIt` |
| The tree gate | the one-shot path carries only the feedable shapes | `SolidFoodAdmissionTests.TheOneShotTree_LeavesTheReplacementShapeOutOfItsReach`, `TheAutoSelect_SkipsTheReplacementShapeAndPicksTheFoodBehindIt` |
| The request half | the host commits nothing and asks the eater to run it | `ItemUseTests.Host_OffersBreadToGuest_AsksTheEaterToRunTheGamesOwnEat` |
| The outcome half | the reported state lands on the OWNER and is published as the ordinary result | `ItemUseTests.Guest_EatsTheBreadAndReportsTheOutcome_TheHostHandsItToItsOwner` |
| The consume shape | the item's own action destroying it removes it from its owner | `ItemUseTests.Guest_EatsAFoodWhoseUseConsumesIt_AndItsOwnerLosesTheItem` |
| The admission | an unadmitted report changes nothing, and one admission settles once | `ItemUseTests.EatOutcome_WithoutAnAdmittedEat_ChangesNothing`, `Guest_EatsTheBread_TwiceReportsOnce` |
| The refusal | the replacement shape is refused by name | `ItemUseTests.Use_OfAFoodThatHandsTheEaterAReplacement_IsRefused` |
| The host as the affected side | its own eat's report is handled from inside the first result's projection (the nested publish) | `ItemUseTests.Host_EatsAsTheAffectedSide_AndItsOutcomeIsHandledFromInsideTheResultProjection` |
| The eat's shape | the game's own `UseItem` on a standing object, inside the item-use sound scope, asked for by name only on the request half | `SolidFoodChainGateTests.TheEat_RunsOnTheAffectedSideThroughTheGamesOwnUseEntry` |
| The report's route | the standing branch reports instead of publishing as this side's own fact | `SolidFoodChainGateTests.TheEatingSidesReport_IsTheStandingObjectsOwnRouteToItsOwner` |
| No successor table | the chain's Runtime sources hold no id-keyed table | `SolidFoodChainGateTests.TheRuntimeSolidFoodChain_HoldsNoIdKeyedItemTable` (+ its matcher samples) |
| The deleted table's census | the directory's remaining catalogs are an exact set again | `WearChainContentGateTests.TheRuntimeWearChain_HoldsNoIdKeyedItemTable` (the food row removed from `PendingCatalogFiles`) |

## 4. Verification design and result

What this cycle's own run proves (commands and numbers measured on the frozen tree):

- `dotnet build CasualtiesUnknownOnline.slnx` — 0 warnings, 0 errors;
- `dotnet format CasualtiesUnknownOnline.slnx` — exit 0 (run once, before the gates);
- `dotnet test CasualtiesUnknownOnline.slnx` with the build — behaviour 4805/4805 and normative gates
  547/547 with the delivery-checklist gate EXCLUDED. That gate is the 548th and it is red until this
  cycle's boxes are all checked, which is it doing its job rather than a failure: the committing run
  (after the boxes) reports 548/548;
- the focused runs used while iterating: the two chain test classes plus the direction and chain gates.

The message-level design is proven end to end at L0: a real host+guest pair with the real kernel, the
real result projection and the real wire carries the request half, the eater's report and the second
result, including the nesting the host-as-the-affected-side case has (the outcome is handled from inside
the first result's projection). What no L0 case can prove is the game-side half — the standing object's
existence on the eater's client, the delegate's own body writes, and the clips — because that needs the
game's `Body` and `Item` (the suite's own boundary, stated in `docs/acceptance/`).

## 5. Limits recorded

1. **One level of indirection.** The verdict reads the use action's own instructions and those of the
   game's methods it calls directly, which is exactly what the component-driven can needs. A mod whose
   action feeds the body through a helper of its own two calls away is not recognised; it keeps the
   behaviour it has today (the gesture falls through to the native drop) instead of becoming a wrong one.
2. **An unreadable delegate fails closed and loud.** A use action whose compiled body cannot be read
   answers "not a solid food" and logs one line naming the item.
3. **The grant is keyed by (eater, item).** Two eats of the same item in flight at once — by the same
   eater or by two different ones — settle in report order rather than being told apart; a report with
   no grant (or a second one) is refused by name and changes nothing.
4. **A callee whose SOURCE is not in the decompile is still read at runtime.** The verdict merges the
   bodies of the game methods the use action calls directly, and the adapter reads the real assembly, so
   a generic helper the `reversing/` tree does not carry (e.g. `ComponentHolderProtocol.GetOrAddComponent<T>`,
   called by `browncap` and `funguschunk`) is read like any other: an object handed over inside it would
   classify those items as the REFUSED shape, never run one. The runtime scan over the vanilla table
   yields 41 feeding / 2 handing over / 1 destroying, which is what the census names.
4. **The replacement shape is refused, not delivered.** `bucketofchicken` and `popcorn` feed a body but
   hand the eater a replacement object at their last bite; the family refuses them by name, so the
   gesture keeps the behaviour it had before the family existed (a native drop), and the item cannot be
   finished cross-player. `stonefruitclosed` and `rosepod` never call `Body.Eat`/`Body.Drink` at all and
   were never in the deleted table either: nothing about them changed.
5. **The eat needs the standing object to exist on the eater's client.** A client whose fact table has
   not delivered the owner's row yet refuses the eat by name instead of guessing (no object, no meal).
6. **The real-machine readings are the acceptance batch's** (§7 of the ticket): the eater's body really
   moving by the food's own amounts, the item's condition following the bite on its owner's item, and
   everything the standing object itself owes (count, presentation, parked fluid, no `[ERR][Unity:Exception]`).
7. **The nested publish is proven at the message level only.** The host-as-the-affected-side case drives
   the real kernel, wire and projection nesting (the second result is published from inside the first
   one's commit), but not `Body.UseItem` itself, and it does not cover the re-entrancy of the OTHER
   `BatchCommitted` subscribers (the save service, the enemy and carry projections); the result
   projection it does drive is stateless.

## 6. Disposition of the deleted catalog's cases

One by one, because "no case was dropped" is not true as a blanket statement.

| Deleted case | Disposition |
|---|---|
| `RemoteConsumeApplicationTests.ApplyFood_AppliesBreadEffect` | **No successor by construction.** It pinned CUO's transcription of the game's arithmetic (bread's `9f` hunger, `2f` thirst, `0.5f` weight into a `CharacterHealthMsg`). That arithmetic is now the game's own delegate, executed on the eater's client, and cannot be reached from an L0 case — the same reason the topical and drink chains' per-ml cases have none |
| `RemoteConsumeApplicationTests.Catalog_ExposesTheCuratedFoodItems` | **Superseded in a stronger form** by `SolidFoodAdmissionTests`: the family's membership is now the item's own use-action shape, and the successor cases name the vanilla edibles the deleted table never carried (`geofruit`, `browncap`, `internalorgans`) as members, the shapes the path refuses as refusals, and an unknown id as not a family at all |
| `ItemUseTests.Host_UsesBreadOnGuest_AppliesFoodAndSendsResult` | **Rewritten in place, in two halves**: `Host_OffersBreadToGuest_AsksTheEaterToRunTheGamesOwnEat` pins what the host now does (nothing but the admission and the request) and `Guest_EatsTheBreadAndReportsTheOutcome_TheHostHandsItToItsOwner` pins the item's new state reaching its owner through the eater's report |
| the `IsActuallyUsable` half of `CarriedItemUseTree`'s coverage | **Rewritten**: the tree's gate is now the admission rule, pinned by `SolidFoodAdmissionTests.TheOneShotTree_LeavesTheReplacementShapeOutOfItsReach` and `TheAutoSelect_SkipsTheReplacementShapeAndPicksTheFoodBehindIt` |

The deleted types' own behaviour has no case left to rewrite: `RemoteConsumeApplication.ApplyFood` was
pure field arithmetic on a snapshot (the drink chain's cycle already deleted its sibling half), and
`RemoteFoodEffect` was the row type of the table.
