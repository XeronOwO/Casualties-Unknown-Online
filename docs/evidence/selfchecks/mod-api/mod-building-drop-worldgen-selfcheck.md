# Mod building drops and worldgen density — mechanism inventory and self-check

Owner cycle: CUCoreLib migration support — custom building entity drop rules and
automatic world-generation density after the typed building content + entity
spawn seam landed.

Decision: extend the existing plain `ModBuildingDefinition` DTO with authored
drop tables and world-generation density/placement fields, apply the drop
tables to the runtime `BuildingEntity` template, and distribute custom building
entities during the same sealed `WorldGeneration.PlaceCrystals` stream used by
custom item loose spawns. Both sides create the same deterministic buildings;
`BuildingEntity.Start` reports are already suppressed while generation is
active, so no wire message and no new NetMsg are needed.

## 1. Mechanism inventory

| # | Mechanism | Evidence / decision |
|---|---|---|
| 1 | Typed drop DTO | `ModBuildingDrop` in Abstractions carries item id, chance, and condition min/max as plain data; no game or Unity type crosses the mod boundary. |
| 2 | Typed generation enums | `ModBuildingGenerationStyle` (`None`/`Standard`/`DropPod`) and `ModBuildingPlacement` (`Floor`/`Ceiling`/`Wall`) keep placement vocabulary out of GameAdapter. |
| 3 | DTO density fields | `ModBuildingDefinition` carries `SpawnMinPerChunk`, `SpawnMaxPerChunk`, `SpawnLayers`, `GenerationStyle`, `Placement`, `SpawnInGround`, `SurfaceOffset`, and `RandomFlip`, plus the drop lists. |
| 4 | Provider validation | `GameAdapterBuildingContentProvider.TryBind` refuses NaN/Infinity/negative density/surface values, min>max, invalid drop chances and invalid condition ranges before a definition reaches the world. |
| 5 | Stable worldgen snapshot | `GameAdapterBuildingContentProvider.GetDefinitionsForWorldGen()` returns only enabled definitions in ordinal id order, so both peers consume the shared generation random stream in the same order. |
| 6 | Drop table application | `CustomBuildingTemplateFactory` converts authored `ModBuildingDrop` entries into vanilla `ItemDrop` arrays and sets `itemsDropOnDestroy`, `alwaysDrop`, and `itemCategoriesToAdd` on the runtime template. |
| 7 | Distribution | `BuildingWorldGenDistribution` places `Standard` buildings by surface raycast and `DropPod` buildings at random impact points through `Utils.Create`; both are inside the isolated generation stream. |
| 8 | Patch boundary | `WorldGenerationPlaceCrystalsPatch` calls `IPatchBridge.OnCustomBuildingWorldGeneration` before `OnCustomItemWorldGeneration`, then `GameAdapterDomains.BuildingWorldGen`. |
| 9 | No wire | `BuildingEntityStartPatch` / `EntitySpawnSync` already skip reports while `HarmonyTraverse.IsGenerating()` is true; deterministic generation needs no new NetMsg. |
| 10 | Non-goals | Raw GameObject prefab configure hooks remain non-goal at this seam; only string-based `SpawnComponents` and typed DTO fields were part of this delivery. The later `IModBuildingRuntime` component-returning hook seam is documented in `mod-building-runtime-hooks-selfcheck.md`. |

## 2. Whole-family audit

| Family member | Change |
|---|---|
| `ModBuildingDrop` | New Abstractions DTO for one authored building drop. |
| `ModBuildingGenerationStyle` | New Abstractions enum for worldgen style. |
| `ModBuildingPlacement` | New Abstractions enum for placement surface. |
| `ModBuildingDefinition` | Added drop tables, item categories, density/layer/placement fields, and tile-style layer-mask helpers. |
| `GameAdapterBuildingContentProvider` | Added density/drop validation and stable `GetDefinitionsForWorldGen()`. |
| `CustomBuildingTemplateFactory` | Applies authored drops and item categories to the runtime `BuildingEntity`. |
| `BuildingWorldGenDistribution` | New GameAdapter worldgen domain class. |
| `IPatchBridge` / `GameAdapterBridge` | Added `OnCustomBuildingWorldGeneration` forwarding seam. |
| `GameAdapterDomains` | Owns `BuildingWorldGenDistribution`. |
| `WorldGenerationPlaceCrystalsPatch` | Existing `PlaceCrystals` postfix now also distributes custom buildings, in a fixed order before item scatter. |
| Tests | `ModBuildingDefinitionTests`, `BuildingWorldGenProviderTests`. |
| Protocol version | Unchanged (no wire). |

## 3. Self-check table

| Mechanism | Change | Evidence |
|---|---|---|
| DTO round-trip | New drop lists, enums, density, and layer fields survive `ToPayload`/`FromPayload` | `ModBuildingDefinitionTests.RoundTrip_PreservesCoreFields` |
| Null optionals | New optional density/placement fields remain null after round-trip | `ModBuildingDefinitionTests.RoundTrip_PreservesNullOptionalOverrides` |
| Layer helper | `LayersToMask` / `AllLayersExcept` / `CanSpawnInLayer` follow the tile worldgen convention | `ModBuildingDefinitionTests.LayerMaskHelpers_AreConsistent` |
| Drop roll helper | `ModBuildingDrop.RollCondition` clamps into the authored segment | `ModBuildingDefinitionTests.ModBuildingDrop_RollCondition_ClampsIntoSegment` |
| Stable iteration | Enabled worldgen definitions enumerate in ordinal id order and exclude disabled/None styles | `BuildingWorldGenProviderTests.GetDefinitionsForWorldGen_ReturnsStableIdOrderAndFiltersDisabled` |
| Validation | NaN/Infinity/negative density, min>max, negative offset, invalid drops are refused | `BuildingWorldGenProviderTests.TryBind_AcceptsValidWorldGenAndRejectsInvalidDensity` / `TryBind_RejectsInvalidDrops` |
| No wire/protocol regression | Static content and deterministic generation still use existing seams; no NetMsg added | `docs/api/mod-api.md` §4f, full suite green |

## 4. Verification design

- Pure-managed tests for DTO round-trip, layer-mask helpers, and reflective
  GameAdapter provider validation/order (no Unity world needed).
- The actual `Utils.Create` building distribution remains behind the
  GameAdapter worldgen boundary and is covered by the existing patch-contract
  tests plus the generation-time report suppression; no new wire path is
  introduced.
- Static evidence: no new permission bit, no new NetMsg, no Abstractions
  game/Unity type, no CUCoreLib source committed.

## 5. Verification results

| Evidence | Result |
|---|---|
| `dotnet build CasualtiesUnknownOnline.slnx` | 0 warnings / 0 errors |
| `dotnet test CasualtiesUnknownOnline.slnx` | 2106 passed / 0 failed |
| `dotnet format CasualtiesUnknownOnline.slnx` | clean |
| `tools/check-architecture.ps1` | pass |
| `tools/check-event-replay.ps1` | pass |
| `tools/check-entity-event-dispatch.ps1` | pass |
| `tools/check-delivery.ps1` | pass |
