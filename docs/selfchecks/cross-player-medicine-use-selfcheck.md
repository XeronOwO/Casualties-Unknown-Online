# Cross-player medicine/injectable use self-check

Owner cycle: backlog "Cross-player item use" second slice. Decision: add the
curated immediate-effect medicine containers to the existing
`PlayerItemUseRequest`/`PlayerItemUseResult` operation; do not introduce a new
wire message. Opiate/component effects, timed/random medicine branches, and
topical non-injectable liquids remain future slices.

## 1. Mechanism inventory

| # | Mechanism | Evidence |
|---|-----------|----------|
| 1 | `WaterContainerItem.Inject` | `WaterContainerItem.cs:237-261` — proportional drain + `LiquidType.onHealthUse` for `injectable` liquids |
| 2 | Per-liquid medicine formulas | `Liquids.cs:1504-1589` (saline, ringersolution, blood/redblood, antiserum, ceftriaxone, streptokinase) |
| 3 | Existing cross-player use wire | `PlayerItemUseRequestMsg` / `PlayerItemUseResultMsg` (NetMsg 116/117) — reused unchanged |
| 4 | Character snapshot surfaces | `CharacterHealthMsg` + `CharacterLimbMsg` — all changed values ride the existing post-use result |
| 5 | Local body apply | `PlayerInteractionApply.OnPlayerItemUseReceived` + `CharacterDataSync.ApplyHealState` — untouched |
| 6 | Guest transfer table | `IItemControl.UpdateTransferredItem` — same drain path as drink/food |

## 2. Design

- New `RemoteMedicineLiquidEffect` (Runtime): per-ml body/limb coefficients,
  pure data.
- New `RemoteMedicineCatalog` (Runtime): maps known medicine container ids to
  the exact ml the game injects per use and maps supported liquid ids to their
  per-ml effect. Unknown liquids are refused as a whole; `water` is allowed as
  an inert carrier.
- New `RemoteMedicineApplication` (Runtime): applies the plan to the target's
  health and the most-injured limb (same pick rule as cross-player heal).
- `PlayerItemUseService` tries medicine after drink/food; it reuses
  `ApplyDrain` for the liquid/condition update and the existing result message.
- `PlayerInteractionApply` recognizes medicine containers in the local
  use-item selector, so the Online UI and in-world menu expose them without a
  separate action surface.
- **Scope limits** — supported containers: `saline`, `ringersolution`,
  `bloodbag`, `bloodbaghuman`, `antiserum`, `ceftriaxone`,
  `streptokinase`. Opiates, timed/random stimulants, topical non-injectable
  liquids, and wear/tools remain future slices.

## 3. Self-check table (mechanism × change × evidence)

| Mechanism | Change | Evidence |
|-----------|--------|----------|
| Medicine plan | known container drains min(item amount, remaining) proportionally | `RemoteMedicineApplicationTests.Plan_DrawsItemAmountOrEntireSmallStack` |
| Unknown liquid | refused as a whole for a known container | `Plan_RefusesUnknownLiquidEvenForKnownItem` + service test |
| Saline effect | blood volume/viscosity/thirst per ml | `ApplySaline_AppliesBloodVolumeViscosityAndThirst` + service test |
| Antiserum effect | body + most-injured-limb disinfection | `ApplyAntiserum_AppliesBodyAndMostInjuredLimbDisinfection` |
| Ceftriaxone effect | immunity + limb pain | `ApplyCeftriaxone_IncreasesImmunityAndLimbPain` |
| Redblood effect | harmful body + selected-limb muscle loss | `ApplyRedblood_AppliesHarmfulEffectsToSelectedLimb` |
| Host operation | service drains medicine item, saves target, updates transfer table | `PlayerInteractionServiceTests.Guest_InjectSalineOnHost...` |
| UI eligibility | known medicine item appears in local use-item list | `PlayerInteractionApply.IsLocalUseItem` (no projection change) |

## 4. Verification

- **L0 unit**: `RemoteMedicineApplicationTests` (7) +
  `PlayerInteractionServiceTests` +2 medicine cases.
- **Code gates**: `dotnet build` 0 warnings/0 errors, `dotnet test` 1402 green,
  `dotnet format`, check-architecture / check-event-replay /
  check-entity-event-dispatch all pass.
- **Development-period rule**: L0 + static evidence, `no manual acceptance`.
