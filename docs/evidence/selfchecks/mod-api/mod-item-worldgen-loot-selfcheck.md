# Mod item world-spawn / category loot pool — mechanism inventory and self-check

Owner cycle: CUCoreLib migration support — custom item world spawn
distribution and the missing vanilla category loot-pool injection.

Decision: add the two real migration gaps to the existing item content seam
without porting CUCoreLib's explicit fixed drop-source/trader overrides or its
JToken snapshot channel. A mod can now:

- choose a vanilla loot `Category` + `SpawnFrequency` and have the custom item
  appear in the same category loot pools vanilla items use (corpses, building
  guaranteed drops, traders, dev-console spawners), and
- set `WorldSpawnPerChunk` to scatter loose ground items during the isolated
  world-generation stream; both sides generate the same items and the existing
  generation-item snapshot binds them to the host's ids.

## 1. Mechanism inventory

| # | Mechanism | Evidence / decision |
|---|---|---|
| 1 | Typed payload field | `ModItemDefinition.WorldSpawnPerChunk` (`float?`) in Abstractions; the DTO remains plain data with no game/Unity type. |
| 2 | Provider validation | `GameAdapterItemContentProvider.TryBind` rejects NaN/Infinity/negative `WorldSpawnPerChunk` before the definition enters the world. |
| 3 | Category loot-pool injection | The provider adds each bound custom item to `ItemLootPool.pool[category]` with `SpawnFrequency` repeats, exactly like the game's own `ItemLootPool.InitializePool`; re-injection is idempotent and a replaced pool is re-seeded. A positive `WorldSpawnPerChunk` opts the item out of the generic category pool, matching CUCoreLib's fallback rule. |
| 4 | Stable world-spawn snapshot | `GameAdapterItemContentProvider.GetDefinitionsForWorldSpawn()` returns only items with a positive `WorldSpawnPerChunk` in ordinal id order, so both peers consume the shared generation random stream in the same order. |
| 5 | World-gen scatter | `ItemWorldGenDistribution` picks ground points with `Physics2D` and materializes items through `Utils.Create`; it runs from a `WorldGeneration.PlaceCrystals` postfix inside the sealed generation stream. |
| 6 | Patch boundary | `WorldGenerationItemWorldGenPatch` → `IPatchBridge.OnCustomItemWorldGeneration` → `GameAdapterDomains.ItemWorldGen`. |
| 7 | Corpse loot custom prefabs | `NativeItemResourcePatches` now also transpiles `CorpseScript.Start`, so the vanilla direct `Resources.Load` corpse-loot path serves CUO custom item templates (the same cover already existing for `BuildingEntity.Update` and save restore). |
| 8 | Sync | No new wire: generation-time items are picked up by the existing `GeneratedItemAuthority` / `GeneratedItemApplication` host snapshot. |
| 9 | Non-goals | Explicit CUCoreLib fixed drop-source pools (corpse/crate/trader flags) and its asset-backed visuals remain non-goals for this round; the category loot-pool seam covers real custom-item loot demand without adding a new protocol. |

## 2. Whole-family audit

| Family member | Change |
|---|---|
| `ModItemDefinition` | Added `WorldSpawnPerChunk` nullable field. |
| `GameAdapterItemContentProvider` | Added world-spawn validation, stable `GetDefinitionsForWorldSpawn()`, and idempotent `ItemLootPool` category injection. |
| `ItemWorldGenDistribution` | New GameAdapter worldgen domain class. |
| `WorldGenerationItemWorldGenPatch` | New `PlaceCrystals` postfix hook. |
| `IPatchBridge` / `GameAdapterBridge` | Added `OnCustomItemWorldGeneration`. |
| `GameAdapterDomains` | Added `ItemWorldGen`. |
| `NativeItemResourcePatches` | Added `CorpseScript.Start` transpiler. |
| Tests | `ModItemDefinitionTests`, `ItemWorldGenProviderTests`. |

## 3. Self-check table

| Mechanism | Change | Evidence |
|---|---|---|
| DTO round-trip | `WorldSpawnPerChunk` survives `ToPayload`/`FromPayload` | `ModItemDefinitionTests.RoundTrip_PreservesCoreFields` |
| Stable iteration | World-spawn definitions enumerate in ordinal id order and exclude disabled items | `ItemWorldGenProviderTests.GetDefinitionsForWorldSpawn_ReturnsStableIdOrderAndFiltersDisabled` |
| Validation | NaN/Infinity/negative world-spawn values are refused | `ItemWorldGenProviderTests.TryBind_AcceptsValidWorldSpawnAndRejectsInvalidValues` |
| Patch contract | New `PlaceCrystals` and `CorpseScript.Start` patches resolve against the real game assembly | `PatchContractTests` |
| No wire/protocol regression | Static content and generation-time items still use existing seams; no NetMsg added | `docs/api/mod-api.md` §4f, full suite green |

## 4. Verification results

| Evidence | Result |
|---|---|
| `dotnet build CasualtiesUnknownOnline.slnx` | 0 warnings / 0 errors |
| `dotnet test CasualtiesUnknownOnline.slnx` | 2090 passed / 0 failed |
| `dotnet format CasualtiesUnknownOnline.slnx` | see final gate result |
| `tools/check-architecture.ps1` | pass |
| `tools/check-event-replay.ps1` | pass |
| `tools/check-entity-event-dispatch.ps1` | pass |
| `tools/check-delivery.ps1` | pass |
