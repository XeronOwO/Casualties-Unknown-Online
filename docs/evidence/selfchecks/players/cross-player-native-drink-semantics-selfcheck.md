# Cross-player drink semantics from the game's own data self-check

Owner cycle: `todo/mod-cross-player-native-semantics.md` Part B, the consume
(drink) chain. Decision: drinking stops carrying a table — the game's own
`ItemInfo.usable` flag answers WHO may be given a drink, the operator's client
runs the item's own `useAction` and diverts the native
`WaterContainerItem.Drink` call to MEASURE the ml, and the patient's client runs
each liquid's own `onDrink` on its own body. The host keeps the allowed
operation, the resource and the arbitration. Same shape as Part A (injection) and
Part B's topical chain, one chain per deliverable — except for SOLID food, which
is cut to its own ticket because its native `useAction` writes the eating body
and the item directly and needs an item instance the affected side does not have
(`done/mod-cross-player-solid-food-semantics.md`).

## 1. Mechanism inventory

| # | Mechanism | Evidence |
|---|-----------|----------|
| 1 | The native drink implementation | `WaterContainerItem.cs:198-215` `Drink(Body body, float amount = 100f, string sound = "drink")`: `CalculateDrain(amount)` (proportional, capped) → per stack `if (Liquids.Registry.TryGetValue(...)) onDrink(list[i], body)` → `Drain(list)` (<0.5 ml entries removed) → `Sound.Play(sound, body.transform.position, …)` |
| 2 | `Drink` is the only one of the three container calls with NO content gate | `ApplyToLimb` gates on `healthUsable` (`WaterContainerItem.cs:228`), `Inject` on `injectable` (`:254`), `Drink` gates on nothing (`:205-212`) — so the LIQUID decides nothing here and an unknown/effect-less liquid is the game's own case |
| 3 | The native use dispatch that reaches it | `Body.UseItem(Item)` = `if (item.Stats.usable) item.Stats.useAction(this, item)` (`Body.cs:2475-2481`), called from `PlayerCamera.TryPerformRadialAction`'s radial-centre release (`PlayerCamera.cs:1646`) |
| 4 | The per-item amount is not data | `Drink(body, 100f, "drink")` for a water bottle (`Item.cs:3171`), `20f` for naltrexone (`:962`), `5f` for sleeping pills (`:1422`), `60f` for mindwipe (`:1351`) — `ldc.r4` literals inside each `useAction` delegate; `ItemInfo` has no nutrition/volume field at all |
| 5 | The drink class is a `LiquidItemInfo` with `usable` set | the census of `Item.cs`'s `SetupItems`: **36** vanilla ids (12 drinkable medicines, 4 medical containers that are also usable, 17 drinks, 3 utility/crafting containers); the other liquid containers are `usable=false` (`paincream`, `syringe`, `morphine`, `disinfectant`, …) and are the limb families' |
| 6 | What was deleted | `RemoteDrinkMedicineCatalog` (12 item amounts + 13 per-ml liquid effects + the mindwipe mirror), `RemoteDrinkMedicineApplication`, `RemoteDrinkMedicineEffect`, `RemoteConsumeCatalog`'s 14-liquid table and `DrinkAmountMl` constant, `RemoteConsumeApplication`'s drink plan/effect, `RemoteLiquidEffect`, and the whole `TimedBodyEffectMsg` chain (`PlayerItemUseResultMsg.TimedBodyEffects`, `PlayerInteractionTimedBodyEffect`, `WirePlayerInteractionTimedBodyEffect`, `TimedBodyEffectApply` and its four remaining branches), which had no producer left |
| 7 | What replaced it | Runtime: the new `IConsumeSemantics` seam, `ConsumeAdmission` (the one admission rule), `PlayerItemUseResultMsg.DrinkDose` + its journal/wire twins. Adapter: `GameConsumeFacts` / `GameConsumeSemantics`, `RemoteDrinkUseHandler` (the measurement), `Patches/RemoteDrinkPatches.cs` (the divert), `NativeDrinkApply` (the effect) |
| 8 | The mindwipe gate is in the item delegate, and it is not alone | `Item.cs:1343-1352` refuses while the item holds mindwipe AND `body.totalHappiness > -50f && body.brainHealth > 90f && body.strokeAmount < 5f`; the liquid adds a second gate (`Liquids.cs:1155` returns when the body already has a `MindwipeScript`) |
| 9 | The dose rides existing fields | the measured ml rides `PlayerItemUseRequestMsg.DoseMl` (field 4, added by Part B's topical chain) and `RemoteInventoryIntentMsg.Amount`; the committed drain rides `PlayerItemUseResultMsg.DrinkDose` (field 13, new) plus `PlayerInteractionLiquidStack` in the journal and a `WireLiquidStack` member (23) on the wire |
| 10 | Liquid-level clips are already classified where the patient runs them | `CharacterSoundPolicy`'s `ItemUse` origin classifies the ingest clips — its own comment names "the container drink's `"drink"` / `"pills"`" — and `NativeDrinkApply` opens exactly that scope |

## 2. Whole-family audit

| Mechanism | Verdict |
|---|---|
| Host admission | `ConsumeAdmission` asks the game's data (`LiquidItemInfo.usable` + the item still holding liquid); `CarriedItemUseTree.IsActuallyUsable` uses the same rule, so the auto-select half cannot drift from the explicit-instance half |
| Host drain | `PlayerItemUseService` keeps `LiquidDrainPlan` and the committed-ml arithmetic and writes NO target state: the drink branch sets `drinkDose`, the target save's guard asks BOTH dose members (`if (appliedDose is null && drinkDose is null)`), and the result publishes `Health = null` / `Limbs = []` |
| Patient apply | `PlayerInteractionApply` runs `NativeDrinkApply` for a message that carries a drink dose — outside the `RemoteApply` scope and inside the item-use sound scope, so the clips the liquids play from `onDrink` are relayed exactly as a local drink's are |
| Operator entry — world drag | `CrossPlayerDragUse` measures on the affected player's own render clone, refuses by name when the clone is not rendered, and sends the measured ml |
| Operator entry — held remote item (the wound-view release) | `RemoteDragMutationPatches.RemoteDragApplyWoundItemPatch` measures the dragged proxy on the requester's own body (the body that drinks) and the ml rides the existing intent `Amount` |
| Operator entry — medical view | untouched, and it cannot see a drink: `LocalUseItemEligibility.IsMedicalLimbUseItem` admits the limb-treatment families only, so a drink container never reaches the limb gesture |
| Measurement family order | the operator's measurement sites ask ONE verdict, `LocalUseItemEligibility.FamilyOf`, in the host chain's own order (injectable → topical → drink), so a measurement can never run an item's own action for a family the host refuses — the blocker this cycle's review found was exactly that, for the two vanilla blood bags whose `useAction` is `Item.DrawBlood` (`Item.cs:1762-1765`, which really fills the operator's bag and drains the limb). This half is adapter-side and needs a live `Item`, so no L0 case reaches it; the Runtime half of the same order IS pinned at the production site (`ItemUseTests.Use_ADrinkableInjectableContainer_IsRefusedByTheInjectionFirstOrder`) |
| Sibling chains | wear, solid food, limb tool, bandage/shrapnel/other-medical and the two limb-use families are untouched; the routing ORDER that separates the dual-class containers (saline, ringersolution, blood bags) from the drink rule is pinned at the production site |
| Sound | no drink clip played cross-player before this change, and none is carried now; what IS newly native is the liquid-level clip inside `onDrink` (`pills` for braingrow/antidepressants/antibiotics/antirad/sleepingpills, `drink` for streptokinase), which the patient's own scope relays |
| Tests/tools/docs | the two deleted catalogs' cases are dispositioned one by one in §6; the harness gained `FakeConsumeSemantics` and `ConsumeSemanticsTests`; three gate censuses were reviewed (patch-bridge ports, the adapter capability catalog, and the delivery checklist) |

## 3. Self-check table

| Mechanism × change × evidence | Case |
|---|---|
| Admission from the game's own data (the item's flag, not a liquid allowlist) | `ConsumeSemanticsTests.Admission_AcceptsADrinkContainerHoldingLiquid`, `..._AcceptsALiquidTheDeletedAllowlistNeverCarried`, `..._RefusesAnItemTheGameOnlyDrawsFromALimb`, `..._RefusesAnEmptyContainer` |
| Both rules answer for a container the game marks both ways, so only the routing ORDER separates them | `ConsumeSemanticsTests.TheDrinkRuleAndTheLimbRuleBothAnswerForTheContainersTheGameMarksBothWays` (the FAKE's content only — see its doc comment) and `ItemUseTests.Use_ADrinkableInjectableContainer_IsRefusedByTheInjectionFirstOrder` (the production one-shot chain's order; the operator's own measurement sites ask the same order through `LocalUseItemEligibility.FamilyOf`, which no L0 case can drive) |
| The host caps the measured dose at what the item really carries, and refuses a gesture that measured nothing | `ConsumeSemanticsTests.TheHostCapsTheDoseAtWhatTheItemReallyCarries`, `..._TheHostRefusesADrinkWhoseGestureMeasuredNothing`, `MedicalToolApplicationTests.Use_ADrinkRequestWithNoMeasuredDose_IsRefused` |
| The dose reaches the patient and the host invents no body state (rewritten from the case that pinned the host's own per-ml math) | `ItemUseTests.Guest_UsesWaterOnHost_CommitsTheDoseAndCarriesItToThePatient` (asserts the single dose, `Health is null`, `Limbs` empty, the topical dose member empty, and the host's own snapshot still at 0) |
| A drink medicine's own delegate amount is what travels (rewritten from the timed-effect case) | `MedicalToolApplicationTests.Guest_UsesAntiradOnHost_CarriesTheMeasuredDrinkDose`, `..._Guest_UsesSleepingPillsOnHost_CarriesTheMeasuredDrinkDose`, `..._Guest_UsesMindwipeOnHost_CarriesTheMeasuredDrinkDose` |
| The held-remote-item route commits the dose for the requester (rewritten from the cases that pinned the host's drink arithmetic) | `RemoteUseOnSelfTests.Host_UsesRemoteHeldGuestWaterOnSelf_CommitsTheDoseForTheHost`, `..._Host_UsesRemoteHeldGuestItemInsideNestedContainerOnSelf_FindsRecursively`, `..._Guest_UsesRemoteHeldHostWaterOnSelf_ReverseDirectionWorks`, `..._Host_UsesRemoteHeldItemFromUnconsciousGuest_AllowsOwnerUnconscious` |
| An item no rule claims is refused by the game's own flags | `ItemUseTests.Use_AnItemNoChainClaims_IsRefused` |
| The journal round-trips the drink dose | `PlayerDomainKernelTests.WireBatchRoundTrip_PreservesPlayerItemUseResultEvent` (extended) |
| The food chain is untouched by this migration | `RemoteConsumeApplicationTests.ApplyFood_AppliesBreadEffect`, `..._Catalog_ExposesTheCuratedFoodItems`, `ItemUseTests.Host_UsesBreadOnGuest_AppliesFoodAndSendsResult` |
| The seam does not widen the patch bridge, and the new patch is claimed by a capability | `PatchBridgePortShapeGateTests` (two new members pinned in the medical port and the aggregate's surface) and `AdapterCapabilityCatalogTests` (the items capability claims `RemoteDrinkPatches`) |

## 4. Verification design and result

- Focused: `dotnet test … --filter "FullyQualifiedName~ConsumeSemanticsTests|FullyQualifiedName~ItemUseTests|FullyQualifiedName~RemoteUseOnSelfTests|FullyQualifiedName~MedicalToolApplicationTests|FullyQualifiedName~RemoteConsumeApplicationTests|FullyQualifiedName~TopicalSemanticsTests|FullyQualifiedName~PlayerDomainKernelTests|FullyQualifiedName~InjectionSemanticsTests|FullyQualifiedName~WearTests|FullyQualifiedName~RemoteLimbToolApplicationTests"`
  — green (112 cases).
- Full WITH build: `dotnet test CasualtiesUnknownOnline.slnx` — behaviour **4774/4774**
  (net48) and gates **480/481** (net8.0) with the single red being this checklist's own
  pending boxes, one run per project, `--no-build` on neither. (The standalone gate run
  after they were ticked reads 481/481.)
- `ProtocolVersion.Current` is NOT bumped and no new wire ID was added: the
  protocol number is still the frozen pre-release baseline and the addition
  (`DrinkDose`, field 13 / wire member 23) sits inside a message and an event
  that already exist. The compatibility boundary stays the handshake.
- Coverage direction, stated rather than implied: every L0 case here is the
  GUEST-operator direction (a guest uses its own item on the host). The
  host-operator direction shares the same host entry (`HandleUseRequest` with the
  local SteamId) and the same patient apply, but no case drives it, and no case
  drives a third peer. Both are acceptance-batch rows in the ticket.
- The native half is NOT verifiable in L0: the operator's measurement (running
  the item's own `useAction`), the `Drink` divert, `NativeDrinkApply` and the
  liquid-level clip relay all need a game process. Those rows belong to a later
  agent acceptance batch; the L0 half pins the wire, the drain arithmetic and the
  verdict, and the gates pin that the native half and its capability claim exist.
- Process note: the independent review (fresh context, frozen tree, its report in
  `.agent-local/reviews/drink-native-semantics-review.md`) found ONE blocker — the
  operator's two measurement sites asked topical-then-drink without the injection
  rule, so the two vanilla blood bags' `useAction` (`Item.DrawBlood`, which really
  fills the operator's bag and drains the treated limb) ran before the gesture was
  refused, falsifying this self-check's own limit — plus six minors and four nits.
  All are fixed in the same commit: the family order lives in ONE verdict
  (`LocalUseItemEligibility.FamilyOf`) that every measurement site asks, the target
  save's guard asks both dose members, the reused dose fields' docs name the three
  migrated families, a non-finite dose is refused by `LiquidDrainPlan`, and the
  counts and claims in this self-check, the ticket, the harness fake and the
  delivery checklist were corrected.

## 5. Limits recorded

- **An item in the drink class whose delegate is not a drink runs its own action
  on the operator's client and is then refused — except the two the injection rule
  claims.** The class is the game's own flag, and no data says which native call a
  delegate makes, so the measurement can only find out by running it: the divert
  captures a `Drink` call or it does not, and when it does not the gesture is
  refused by name — after the delegate's own action has already run there. The
  measurement sites ask the injection rule FIRST (`FamilyOf`), so the two vanilla
  blood bags (`bloodbag`, `bloodbaghuman`, whose `useAction` is `Item.DrawBlood` —
  a real fill of the operator's bag and a real drain of the treated limb) are
  never measured: the host refuses them by name, exactly as before this chain
  migrated. What remains exposed is `liquidcentrifuge`: a usable `LiquidItemInfo`
  whose `useAction` runs the centrifuge (`Item.cs:5667`), reachable from BOTH the
  world drag and the wound-view release. Dragging a liquid centrifuge onto a
  teammate therefore separates the operator's own centrifuge contents and then
  refuses the use. A mod item in the same state is exposed the same way. The
  alternative — a hand-written list of which usable containers are drinks — is the
  table this ticket deletes.
- **The mindwipe health gate moved from the host's mirror to the operator's own
  native run.** The item-level gate reads the DRINKING body's
  `totalHappiness`/`brainHealth`/`strokeAmount` and therefore runs on the
  operator's client against the affected player's own displayed body; a request
  that carries no dose is refused by the host, and the liquid-level gate
  (`MindwipeScript` already present) still runs on the patient. A modified
  operator client can therefore assert a dose for a target its own copy calls
  healthy — the same trust boundary the dose VALUE already has. The deleted host
  mirror closed that hole entirely, and the host could not keep it once the gate's
  inputs became the patient's own picture.
- **The world-drag measurement refuses when the affected player's render clone is
  not rendered on the operator's client** (a joiner mid-entry): the clone IS the
  body the item's own `useAction` is handed, and an item delegate may read it, so
  answering from this client's own body would be using local state for a remote
  fact. The topical limb measurement keeps its `selectedLimb` fallback, because
  its body argument is the clip's position rather than a gate's input.
- **The dose's VALUE is the operator's own client assertion**, exactly as Part
  B's topical chain records: what is captured is the amount argument the item's
  delegate passed (100 for a water bottle, 20 for naltrexone, 5 for sleeping
  pills), not what the container holds or would drain. The truth-preserving step
  is the host's cap, `LiquidDrainPlan`'s `Math.Min(amount, total)` over the
  authoritative stacks.
- **The trailing item-level clip of the native drink is not replayed.**
  `WaterContainerItem.Drink` plays its `sound` argument (`"drink"` / `"pills"`)
  at the end; the divert swallows that call on the operator's client and the clip
  is not carried, so neither side plays it. No cross-player drink clip played
  before this change either. The LIQUID-level clips some `onDrink` bodies play
  ARE native now and are relayed from the patient.
- **A dose the patient cannot apply** (a liquid the registry does not know — the
  only way in is a broken or mod-authored item — or a liquid with no `onDrink`
  delegate) is logged and skipped AFTER the host has committed the ml and drained
  the item: the resource is spent without an effect. Native `Drink` would throw
  on the delegate-less liquid; this path does not take the patient's client down
  with it.
- **Two eligibility changes are deliberate and user-visible**, both from asking
  the item's own flag instead of the deleted liquid allowlist:
  (1) a limb-usable container the game cannot USE (a `spraybottle` or a `syringe`
  refilled with a drinkable liquid) is no longer fed to a teammate — it is not a
  drink for the game either (`usable` is false, which is why its own use action
  never runs locally), so the drag release falls through to the native drop. This
  reverses the tail Part B's topical chain left behind and the topical self-check
  recorded as an anomaly of the deleted id table.
  (2) the containers the game marks BOTH ways (`saline`, `ringersolution`,
  `bloodbag`, `bloodbaghuman`) stay refused by name on the one-shot path, exactly
  as before, because the limb rules are asked first — now pinned by a case.
- **The journal dedupes the result by operation id**, so the ordinary missed-range
  replay cannot double-apply the dose; the residual `Restore` window is the
  family's, recorded with Part A and Part B's topical chain, and a real session
  must settle it.
- **Solid food is NOT migrated here** and still runs the curated `Food` table on
  the host. Its native `useAction` is a different shape — it calls `body.Eat` /
  `body.Drink` and writes body fields and the item directly
  (`Item.cs:2408-2414`), with no divertible container call and no data field
  carrying the amounts — so the affected side cannot run it without an item
  instance. Its own ticket carries the two candidate designs.
- **The drink class census is a hand-written list in the test harness**
  (`FakeConsumeSemantics`, 36 ids read off `Item.cs`), like the limb chains'
  fake: a readable gate cannot read a game assembly, so a stale entry can only
  make a suite test wrong, never a live session.

## 6. Disposition of the deleted catalogs' cases

`RemoteConsumeApplicationTests` (6 cases) and `RemoteDrinkMedicineApplicationTests`
(16 cases) were the two files that pinned the tables. Case by case, because "no
case was dropped" is not true as a blanket statement: the effect arithmetic they
pinned no longer exists anywhere. Of the 22 rows below, **8 have no successor**
(marked in the table), and **14 are superseded in a stronger form, rewritten in
place or kept** — the counts decompose the 22 exactly.

| Deleted case | Disposition |
|---|---|
| `RemoteConsumeApplicationTests.DrinkPlan_TakesFullAmountFromFullContainer` | Superseded, in a stronger form: the per-item amount is no longer a constant, so the dose is the operator's measurement and the host caps it — `ConsumeSemanticsTests.TheHostCapsTheDoseAtWhatTheItemReallyCarries` and `ItemUseTests.Guest_UsesWaterOnHost_CommitsTheDoseAndCarriesItToThePatient`. |
| `RemoteConsumeApplicationTests.DrinkPlan_TakesEntireSmallContainer` | Same as the row above (the cap is the whole stack when it holds less). |
| `RemoteConsumeApplicationTests.DrinkPlan_RefusesUnknownLiquid` | Superseded: the liquid decides nothing now — `ConsumeSemanticsTests.Admission_AcceptsALiquidTheDeletedAllowlistNeverCarried` pins the opposite, and `ItemUseTests.Use_AnItemNoChainClaims_IsRefused` pins the refusal that does exist. |
| `RemoteConsumeApplicationTests.ApplyDrink_AppliesWaterThirstAndTemperature` | **No successor — dropped because native.** It pinned CUO's transcription of the water liquid's `onDrink` per-100-ml coefficients (`Liquids.cs:171`); that arithmetic is now the game's own delegate, executed on the patient's client, and no L0 case can reach it (it needs the game's `Body`). |
| `RemoteConsumeApplicationTests.Catalog_ExposesCuratedFoodAndLiquids` | Rewritten in place: the catalog now answers for SOLID food only, and the drink half's verdict is the game's own flag (`ConsumeSemanticsTests`). |
| `RemoteConsumeApplicationTests.ApplyFood_AppliesBreadEffect` | Kept: the solid-food branch is unchanged by this migration and still runs on the host. |
| `RemoteDrinkMedicineApplicationTests.Catalog_ExposesDrinkableMedicineItemsAndLiquids` | Superseded, in a stronger form: the item census is the game's own registry now (`ConsumeSemanticsTests.Admission_*` and the harness's `FakeConsumeSemantics`), not a 12-id list. |
| `RemoteDrinkMedicineApplicationTests.Plan_DrawsItemDrinkAmountOrEntireSmallStack` | Superseded: the amount is the delegate's own literal, measured — `MedicalToolApplicationTests.Guest_UsesAntiradOnHost_CarriesTheMeasuredDrinkDose` and `ConsumeSemanticsTests.TheHostCapsTheDoseAtWhatTheItemReallyCarries`. |
| `RemoteDrinkMedicineApplicationTests.Plan_MindwipeMixedContainer_DrawsProportionalWholeStack` | Superseded: the proportional draw across a mixed container is `LiquidDrainPlan`, whose arithmetic the two limb chains and this one share (`TopicalSemanticsTests.TheHostDrawsTheDoseTheItemsOwnDelegateComputed`, `ConsumeSemanticsTests.TheHostCapsTheDoseAtWhatTheItemReallyCarries`). |
| `RemoteDrinkMedicineApplicationTests.Plan_RefusesUnknownLiquidForKnownDrinkableMedicine` | Superseded: the item's own flag admits the gesture and the patient skips a liquid its registry does not know (recorded as a limit); the host's refusal is the no-dose one, pinned by `MedicalToolApplicationTests.Use_ADrinkRequestWithNoMeasuredDose_IsRefused`. |
| `RemoteDrinkMedicineApplicationTests.ApplyPainkillers_AddsOpiateAmount` | **No successor — dropped because native.** It pinned CUO's transcription of `Liquids.cs:293-302`; the arithmetic is the liquid's own `onDrink` now, run on the patient. |
| `RemoteDrinkMedicineApplicationTests.ApplyAntibiotics_AddsImmunityAndAdjustsSepticAndHappiness` | **No successor — dropped because native** (same reason; `Liquids.cs:1178-1191`). |
| `RemoteDrinkMedicineApplicationTests.ApplyKeratinBooster_NormalBranchAddsFullClawRegrowTime` | **No successor — dropped because native**, and the branch itself moved: the `clawRegrowTime > 3600` overdose test is the liquid's own code on the patient (`Liquids.cs:1238-1266`). |
| `RemoteDrinkMedicineApplicationTests.ApplyKeratinBooster_OverdoseBranchAddsReducedRegrowAndSickness` | Same as the row above. |
| `RemoteDrinkMedicineApplicationTests.ApplyBraingrow_WithExistingBrainGrowSetsMindwipeAndShock` | **No successor — dropped because native** (`Liquids.cs:1119-1148`, including the per-call random vomit roll, which follows the patient's own body now). |
| `RemoteDrinkMedicineApplicationTests.ApplyMindwipe_SetsMindwipeScriptPresent` | **No successor — dropped because native** (`Liquids.cs:1150-1163`, the component add). |
| `RemoteDrinkMedicineApplicationTests.ApplySleepingPills_AddsComponentAmount` | Superseded in part and dropped in part: the dose that reaches the patient is pinned by `MedicalToolApplicationTests.Guest_UsesSleepingPillsOnHost_CarriesTheMeasuredDrinkDose`; the component amount it applied is the liquid's own `GetOrAddComponent<SleepingPills>` on the patient. |
| `RemoteDrinkMedicineApplicationTests.ApplyNaltrexone_AdjustsPainkillerComponentsAndHappiness` | **No successor — dropped because native** (`Liquids.cs:350-373`, including the timed op and its random roll). |
| `RemoteDrinkMedicineApplicationTests.BuildTimedEffects_Antirad_ProducesScaledDuration` | Superseded, in a stronger form: the timed body is the liquid's own `CoUtils.DoTimedOp("antirad", …)` started by the patient's `onDrink`, and the case that used to assert the CUO message now asserts the measured dose on the wire (`Guest_UsesAntiradOnHost_CarriesTheMeasuredDrinkDose`). |
| `RemoteDrinkMedicineApplicationTests.BuildTimedEffects_Braingrow_ProducesConstantDurationAndDose` | Same as the row above (`Liquids.cs:1122`). |
| `RemoteDrinkMedicineApplicationTests.BuildTimedEffects_Antidepressants_ProducesOneShotWithDose` | Same as the row above (`Liquids.cs:1173`); the whole `TimedBodyEffectMsg` chain is deleted, so the assertion has no subject left. |
| `RemoteDrinkMedicineApplicationTests.MindwipeBlocked_OnlyWhenTargetMentallyHealthy` | Superseded: the gate is the item delegate's own (`Item.cs:1343-1352`), run on the operator's client against the affected body; the host sees a dose or no dose — `MedicalToolApplicationTests.Guest_UsesMindwipeOnHost_CarriesTheMeasuredDrinkDose` and `..._Use_ADrinkRequestWithNoMeasuredDose_IsRefused`. |

Cases rewritten in place rather than deleted, named here so the rewrite is not
silent: `ItemUseTests.Guest_UsesWaterOnHost_…` (was
`…_AppliesDrinkAndSendsResult`), `ItemUseTests.Guest_UseResult_ProjectsUseEventOnBothParticipants`
(now carries the measured dose), `ItemUseTests.Use_ADrinkableInjectableContainer_…`
(replaces `Use_UnknownMedicineLiquid_IsRefused`, whose subject — the liquid
allowlist — no longer exists), `ItemUseTests.Use_AnItemNoChainClaims_IsRefused`
(was `Use_ALiquidNoChainClaims_IsRefused`), the four `RemoteUseOnSelfTests` drink
cases, and the four `MedicalToolApplicationTests` drink cases.
