# Cross-player drinkable medicine self-check

Owner cycle: backlog "Cross-player item use" remaining drinkable timed/random/component
medicine branches. Decision: extend the existing
`PlayerItemUseRequest`/`PlayerItemUseResult` operation with a dedicated
drinkable-medicine catalog/application and the existing `TimedBodyEffectMsg`
list (additive `DoseMl` only). No new NetMsg and no `ProtocolVersion` bump.

## 1. Mechanism inventory

| # | Mechanism | Evidence |
|---|-----------|----------|
| 1 | Native drinkable medicine `useAction` | `Item.cs:960-963` (naltrexone), `982-985` (sodiumnitroprusside), `1004-1007` (vasopressin), `1026-1029` (amiodarone), `1107-1110` (painkillers), `1131-1134` (keratinbooster), `1154-1157` (braingrow), `1296-1299` (antidepressants), `1319-1322` (antibiotics), `1343-1352` (mindwipe gate), `1397-1400` (antirad), `1420-1423` (sleepingpills) |
| 2 | Liquid `onDrink` formulas | `Liquids.cs:356-373` (naltrexone), `1432-1435` (sodiumnitroprusside), `1449-1453` (vasopressin), `1476-1480` (amiodarone), `298-302` (painkillers), `1256-1266` (keratinbooster), `1119-1148` (braingrow), `1170-1176` (antidepressants), `1184-1191` (antibiotics), `1150-1164` (mindwipe), `1273-1287` (antirad), `1294-1299` (sleepingpills), `222-226` (morphine drink) |
| 3 | Saveable body components | `SleepingPills.cs` (`amount`), `Antidepressants.cs` (`amount`/`currentAmount`/`TakeDose`), `MindwipeScript.cs` (`active`, wipe routine) — Mapster cannot map them, so `CharacterHealthMsg` gains the medication component fields |
| 4 | Existing cross-player use wire | `PlayerItemUseRequestMsg` / `PlayerItemUseResultMsg` (NetMsg 116/117) — reused unchanged; `TimedBodyEffectMsg.DoseMl` is an additive protobuf field |
| 5 | Host authority | `PlayerItemUseService` already routes known item ids; timed/random/one-shot branches are deliberately NOT simulated by the host — the target's local body runs them and reports back |
| 6 | Local target apply | `TimedBodyEffectApply` (Game Adapter) gains `antirad`, `naltrexone`, `braingrow` and `antidepressants` cases; `MedicationComponentsSync` applies SleepingPills/Antidepressants/MindwipeScript component state |

## 2. Design

- `RemoteDrinkMedicineCatalog` maps the 12 native drinkable medicine item ids
  to their per-use ml and the supported `onDrink` liquids to pure per-ml
  effects. `mindwipe`'s mental-health gate is mirrored in
  `IsMindwipeBlocked`.
- `RemoteDrinkMedicineApplication.Apply` applies the immediate/deterministic
  body/component deltas to the target `CharacterHealthMsg`; keratin and
  braingrow conditional branches are represented explicitly.
- `RemoteDrinkMedicineApplication.BuildTimedEffects` produces
  `TimedBodyEffectMsg` for antirad, naltrexone, braingrow and antidepressants;
  the new `DoseMl` carries the drawn ml for the target-side lambdas/one-shots.
- `CharacterHealthMsg` gains `SleepingPillsAmount`,
  `AntidepressantsAmount`, `AntidepressantsCurrentAmount`,
  `MindwipeScriptPresent` and `MindwipeScriptActive`; a new
  `MedicationComponentsSync` plus `CharacterComponentSync` wrap Mapster-invisible
  component capture/apply in the character snapshot and restore paths.
- `PlayerItemUseService` and `PlayerInteractionApply`/`LocalUseItemEligibility`
  recognize the drinkable medicine items through the existing Use UI.
- **No new NetMsg and no `ProtocolVersion` bump** — additive protobuf fields
  only.

## 3. Self-check table (mechanism × change × evidence)

| Mechanism | Change | Evidence |
|-----------|--------|----------|
| Catalog | drinkable medicine item ids/liquids accepted | `RemoteDrinkMedicineApplicationTests.Catalog_ExposesDrinkableMedicineItemsAndLiquids` |
| Drink plan | per-item ml draw and mixed mindwipe/morphine draw | `Plan_DrawsItemDrinkAmountOrEntireSmallStack`, `Plan_MindwipeMixedContainer_DrawsProportionalWholeStack` |
| Immediate apply | painkillers opiate, antibiotics, keratin, sleeping pills | `ApplyPainkillers_AddsOpiateAmount`, `ApplyAntibiotics_...`, `ApplyKeratinBooster_...`, `ApplySleepingPills_...` |
| Conditional apply | braingrow mindwipe/shock, mindwipe trigger | `ApplyBraingrow_WithExistingBrainGrowSetsMindwipeAndShock`, `ApplyMindwipe_SetsMindwipeScriptPresent` |
| Timed/one-shot plan | antirad, braingrow, antidepressants carry TimedBodyEffectMsg + DoseMl | `BuildTimedEffects_Antirad_...`, `BuildTimedEffects_Braingrow_...`, `BuildTimedEffects_Antidepressants_...` |
| Native gate | mindwipe refused while target mentally healthy | `MindwipeBlocked_OnlyWhenTargetMentallyHealthy`, `PlayerInteractionServiceTests.Use_MindwipeOnMentallyHealthyHost_IsRefused` |
| Host operation | guest uses antirad/sleepingpills/mindwipe on host | `PlayerInteractionServiceTests.Guest_UsesAntiradOnHost_...`, `...SleepingPills...`, `...Mindwipe...` |
| Local component sync | new helper surface exists | `MedicationComponentsSyncTests` reflective contract |

## 4. Verification

- **L0 unit**: `RemoteDrinkMedicineApplicationTests` (16),
  `PlayerInteractionServiceTests` +4, `MedicationComponentsSyncTests` (2);
  full suite 1494 green.
- **Code gates**: `dotnet build` 0 warnings/0 errors, `dotnet test` full suite
  green, `dotnet format`, check-architecture / check-event-replay /
  check-entity-event-dispatch all pass.
- **Development-period rule**: L0 + static evidence, `no manual acceptance`.

## 5. Scope limits closed

The previous remaining drinkable timed/random/component medicine line is now
closed. Future extension candidates outside this slice: arbitrary refilled
containers whose item id is not a native drinkable medicine item, and any
non-listed `onDrink` liquid that has no native stand-alone drink item.
