# Cross-player timed/random liquid medicine (injectable) self-check

> **HISTORICAL** — This selfcheck describes a superseded/removed wire path or
> an intermediate architecture slice. It is retained for audit history, not as
> current evidence. Check `docs/selfchecks/MANIFEST.md` and
> `docs/architecture-evolution/protocol.md` before citing.

Owner cycle: backlog "Cross-player item use" remaining timed/random liquid
medicine branches. Decision: extend the existing
`PlayerItemUseRequest`/`PlayerItemUseResult` operation with an additive
`TimedBodyEffectMsg` list so the target's local body runs the native timed
`CoUtils.DoTimedOp` lambdas. No new NetMsg and no `ProtocolVersion` bump.

## 1. Mechanism inventory

| # | Mechanism | Evidence |
|---|-----------|----------|
| 1 | Native injectable containers | `Item.cs:1537-1541` (`bloodcoagulant` → procoagulant, inject 33.334 ml), `1563-1567` (`combatpen` → high stimulant/epinephrine/oxyline, inject 100 ml), `753-759` (`syringe`, inject 100 ml on full minigame success) |
| 2 | Timed/random `onHealthUse` formulas | `Liquids.cs:466-473` (chloroform), `490-497` (high stimulant), `513-567` (mid stimulant), `587-594` (low stimulant), `1308-1321` (procoagulant), `1347-1362` (epinephrine), `1389-1399` (oxyline), `1462-1474` (amiodarone) |
| 3 | Existing cross-player use wire | `PlayerItemUseRequestMsg` / `PlayerItemUseResultMsg` (NetMsg 116/117) — reused unchanged; `PlayerItemUseResultMsg.TimedBodyEffects` is an additive protobuf field |
| 4 | Host authority | `PlayerItemUseService` already routes known injectable containers through the medicine plan; timed/random branches are deliberately NOT simulated by the host — the target's local body runs them and reports back |
| 5 | Local target apply | `TimedBodyEffectApply` (Game Adapter, new top-level class) schedules the exact native `CoUtils.DoTimedOp` lambda on the target body; the ordinary character snapshot paths carry the resulting state back |

## 2. Design

- `RemoteMedicineLiquidEffect` gains `TimedEffectId` and
  `TimedDurationPerMl`; `RemoteMedicineCatalog` adds the timed/random injectable
  liquid entries and the item ids `bloodcoagulant`, `combatpen` and `syringe`.
- `RemoteMedicineApplication.BuildTimedEffects(plan)` produces one
  `TimedBodyEffectMsg` per timed liquid in the drawn plan, with the exact native
  duration (e.g. high stimulant 2.4 s/ml, epinephrine 6 s/ml, oxyline
  2 s/ml, procoagulant 20/33.34 s/ml).
- `PlayerItemUseResultMsg` gains `TimedBodyEffects` (ProtoMember 10); the host
  result carries them only to the target side.
- `PlayerInteractionApply` calls `TimedBodyEffectApply` for the local target.
  The apply switch covers `chloroform`, `highgradestimulant`,
  `midgradestimulant`, `lowgradestimulant`, `procoagulant`, `epinephrine`,
  `oxyline` and `amiodarone`; high/low reuse the native private static
  `Liquids.HighGradeStimulantStep` / `LowGradeStimulantStep` through reflection,
  and the remaining lambdas are ported one-to-one from `Liquids.cs`.
- **Scope limits** — drinkable timed/random/component medicines (antirad,
  sleepingpills, painkillers, antibiotics, antidepressants, braingrow,
  mindwipe, keratinbooster, naltrexone, and other onDrink branches) remain a
  future slice.

## 3. Self-check table (mechanism × change × evidence)

| Mechanism | Change | Evidence |
|-----------|--------|----------|
| Catalog | timed injectable item ids/liquids accepted | `RemoteMedicineApplicationTests.Catalog_ExposesTimedMedicineItemsAndLiquids` |
| Timed plan | combatpen produces three scaled body effects | `BuildTimedEffects_CombatPen_ProducesScaledBodyEffects` |
| Timed plan | bloodcoagulant produces procoagulant timed effect | `BuildTimedEffects_BloodCoagulant_ProducesScaledProcoagulantEffect` |
| Timed plan | immediate-only liquid produces no timed effect | `BuildTimedEffects_ImmediateOnlyLiquid_ReturnsEmpty` |
| Host operation | guest uses combatpen on host, three timed effects in result | `PlayerInteractionServiceTests.Guest_UsesCombatPenOnHost_CarriesTimedBodyEffects` |
| Host operation | guest uses bloodcoagulant on host, one timed effect and drained item | `PlayerInteractionServiceTests.Guest_UsesBloodCoagulantOnHost_CarriesTimedBodyEffect` |
| Wire | result carries timed body effects additively | `PlayerItemUseResultMsg.TimedBodyEffects` asserted in service tests |
| Local apply | target schedules native timed op with exact lambdas | `TimedBodyEffectApply` static adapter surface; L0 evidence (source port) |

## 4. Verification

- **L0 unit**: `RemoteMedicineApplicationTests` +5,
  `PlayerInteractionServiceTests` +2; full suite 1472 green.
- **Code gates**: `dotnet build` 0 warnings/0 errors, `dotnet test` full suite
  green, `dotnet format`, check-architecture / check-event-replay /
  check-entity-event-dispatch all pass.
- **Development-period rule**: L0 + static evidence, `no manual acceptance`.
