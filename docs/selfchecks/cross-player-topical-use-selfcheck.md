# Cross-player topical use self-check

Owner cycle: backlog "Cross-player item use" third slice. Decision: add the
curated topical (non-injectable, `ApplyToLimb`) containers to the existing
`PlayerItemUseRequest`/`PlayerItemUseResult` operation; do not introduce a new
wire message. Opiates/component effects, timed/random stimulants, wear and
tools remain future slices.

## 1. Mechanism inventory

| # | Mechanism | Evidence |
|---|-----------|----------|
| 1 | `WaterContainerItem.ApplyToLimb` | `WaterContainerItem.cs:218-234` — proportional drain + `LiquidType.onHealthUse` for `healthUsable` liquids |
| 2 | Topical item use amounts | `Item.cs:650-653` (paincream 10 ml), `Item.cs:674-677` (woundglue 20 ml), `Item.cs:2097-2102` (disinfectant 10 ml), `Item.cs:2121-2125` (spraybottle 10 ml) |
| 3 | Per-liquid topical formulas | `Liquids.cs:1000-1005` (alcohol), `1036-1043` (bleach), `1065-1075` (reliefcream), `1097-1108` (woundglue), `1639-1643` (disinfectant), `1798-1803` (soap) |
| 4 | Existing cross-player use wire | `PlayerItemUseRequestMsg` / `PlayerItemUseResultMsg` (NetMsg 116/117) — reused unchanged |
| 5 | Character snapshot surfaces | `CharacterHealthMsg` + `CharacterLimbMsg` — all changed values ride the existing post-use result |
| 6 | Local body apply | `PlayerInteractionApply.OnPlayerItemUseReceived` + `CharacterDataSync.ApplyHealState` — unchanged |
| 7 | Guest transfer table | `IItemControl.UpdateTransferredItem` — same drain path as drink/food/medicine |

## 2. Design

- New `RemoteTopicalLiquidEffect` (Runtime): per-ml body/limb coefficients and
  the woundglue multiplicative pain factor, pure data.
- New `RemoteTopicalCatalog` (Runtime): maps known topical item ids to the
  exact ml the game's `ApplyToLimb` drains per use (`paincream`, `woundglue`,
  `disinfectant`, `spraybottle`) and maps the six supported health-usable
  liquids to their immediate per-ml effect. Unknown liquids are refused as a
  whole.
- New `RemoteTopicalApplication` (Runtime): applies the plan to the target's
  health and the most-injured limb (same pick rule as cross-player heal and
  medicine), with `SetDisinfect` modelled as max rather than addition.
- `PlayerItemUseService` tries topical after medicine; it reuses `ApplyDrain`
  for the liquid/condition update and the existing result message.
- `PlayerInteractionApply` recognizes topical containers in the local
  use-item selector, so the Online UI and in-world menu expose them without a
  separate action surface.
- **Scope limits** — supported containers: `paincream`, `woundglue`,
  `disinfectant`, `spraybottle`; supported liquids: `alcohol`, `bleach`,
  `reliefcream`, `woundglue`, `disinfectant`, `soap`. Timed/random branches,
  wear, tools, and opiate component effects remain future slices.

## 3. Self-check table (mechanism × change × evidence)

| Mechanism | Change | Evidence |
|-----------|--------|----------|
| Topical plan | known container drains min(item amount, remaining) proportionally | `RemoteTopicalApplicationTests.Plan_DrawsItemAmountOrEntireSmallStack` |
| Unknown topical liquid | refused as a whole for a known container | `Plan_RefusesUnknownLiquidEvenForKnownItem` + service test |
| Woundglue effect | limb skin/muscle/bandage/infection/pain + body viscosity/sickness | `ApplyWoundglue_AppliesLimbAndBodyEffects` + service test |
| Disinfectant disinfection | max semantics, not additive | `ApplyDisinfectant_UsesMaxNotAdditionForDisinfection` |
| Soap effect | body dirtyness + short disinfection | `ApplySoap_ReducesDirtynessAndSetsShortDisinfection` |
| Host operation | service drains topical item, saves target, updates transfer table | `PlayerInteractionServiceTests.Guest_UsesPaincreamOnHost...` |
| UI eligibility | known topical item appears in local use-item list | `PlayerInteractionApply.IsLocalUseItem` (no projection change) |

## 4. Verification

- **L0 unit**: `RemoteTopicalApplicationTests` (6) +
  `PlayerInteractionServiceTests` +2 topical cases.
- **Code gates**: `dotnet build` 0 warnings/0 errors, `dotnet test` full suite
  green, `dotnet format`, check-architecture / check-event-replay /
  check-entity-event-dispatch all pass.
- **Development-period rule**: L0 + static evidence, `no manual acceptance`.
