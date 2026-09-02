# Mod tile ore/drop/worldgen projection — mechanism inventory and self-check

Owner cycle: CUCoreLib migration support — typed terrain-tile content after the
static tile binding and single-cell placement seams landed.

Decision: extend `ModTileDefinition` with the CUCoreLib-style ore/drop
vocabulary and consume it inside CUO's existing world-generation and
block-break paths. Custom tile distribution runs from a
`WorldGeneration.GenerateOres` postfix, so all Random consumption stays inside
the sealed generation stream. Custom tile drops are spawned locally on a break
inside the existing damage-block scope and ride the existing block-break report;
no wire message, no JObject snapshot, and no game/Unity type in Abstractions.

## 1. Mechanism inventory

| # | Mechanism | Evidence / decision |
|---|---|---|
| 1 | Typed DTO | `ModTileDefinition` now carries `SpawnAmount`, `SpawnLayers`, `ModTileGenerationStyle`, and `List<ModTileDrop>`. Pure Abstractions data contracts; no Unity/game type. |
| 2 | Layer mask helpers | `ModTileDefinition.LayersToMask`, `AllLayersExcept`, `AllSpawnLayers`, and `CanSpawnInLayer(depth)` make the one-based bitmask vocabulary explicit for migrating mods. |
| 3 | Stable worldgen snapshot | `GameAdapterTileContentProvider.GetDefinitionsForWorldGen()` returns accepted definitions sorted by id so both peers consume the shared Random stream in the same order. |
| 4 | Vanilla ore boundary | `WorldGenerationGenerateOresPatch` calls `TileWorldGenDistribution` from the vanilla `GenerateOres` postfix — the same synchronous point as CUCoreLib's custom tile generation, inside the isolated stream. |
| 5 | Direct block-table writes | `TileWorldGenDistribution` writes through `HarmonyTraverse.ReadWorldBlocks`, the same `worldBlocks` array the vanilla ore pass writes; no post-generation relay and no new wire. |
| 6 | Prepared injection | Every distributed tile is first prepared with `GameAdapterTileContentProvider.TryPrepareForPlacement`, which allocates/injects the custom `Tile` into the current `WorldGeneration.tiles` palette. |
| 7 | Generation styles | `Vein`, `HeavyVeins`, `Singular`, `Stripe`, `Inner`, and `Outskirt` are supported as pure deterministic Random consumers. |
| 8 | Custom drops | `GameAdapterTileContentProvider.TrySpawnDrops` resolves the broken custom block index, rolls authored `ModTileDrop` chances/conditions, and calls `Utils.Create` while still inside the `DamageBlockOrigin` scope. |
| 9 | Existing drop report | Because the spawned items are marked as block drops by `UtilsCreateDropPatch`, they fold into the existing pending break report and replicate through the current block-break/drop path. |
| 10 | No wire | No new NetMsg, no protocol bump, no JObject snapshot, no generic content sync. |

## 2. Whole-family audit

| Family member | Change |
|---|---|
| `ModTileDefinition` | Added spawn/layer/style/drop DTO fields and layer-mask helpers. |
| `ModTileGenerationStyle` | New public flags enum matching the CUCoreLib style vocabulary. |
| `ModTileDrop` | New typed drop entry data contract. |
| `GameAdapterTileContentProvider` | Added worldgen stable snapshot, definition-by-index lookup, drop validation, and local drop spawning. |
| `TileWorldGenDistribution` | New GameAdapter worldgen domain. |
| `WorldGenerationGenerateOresPatch` | New Harmony postfix on `GenerateOres`. |
| `WorldGenerationDamageBlockPatch` | Prefix captures original custom block; Postfix spawns authored drops before reporting the break. |
| `IPatchBridge` / `GameAdapterBridge` | Added `OnCustomTileOreGeneration` and `OnCustomTileBroken` seams. |
| `GameAdapterDomains` | Owns `TileWorldGenDistribution`. |
| Tests | `ModTileDefinitionTests` round-trip/helper tests + reflective `TileWorldGenProviderTests`. |
| Protocol version | Unchanged (no wire). |

## 3. Self-check table

| Mechanism | Change | Evidence |
|---|---|---|
| DTO round-trip | New worldgen/drop fields survive the opaque payload | `ModTileDefinitionTests.RoundTrip_PreservesWorldGenerationAndDrops` |
| Layer helpers | Depth/mask edge cases are explicit | `ModTileDefinitionTests.CanSpawnInLayer_HandlesAllZeroAndDepthBounds` and `LayerMaskHelpers_BuildExpectedMasks` |
| Stable worldgen order | Provider snapshot is id-ordered | `TileWorldGenProviderTests.GetDefinitionsForWorldGen_ReturnsStableIdOrder` |
| Drop validation | Invalid authored chance is refused at bind time | `TileWorldGenProviderTests.TryBind_AcceptsValidDropAndRejectsInvalidDropChance` |
| Patch contract | New `GenerateOres` postfix and modified `DamageBlock` patch resolve against the game assembly | `PatchContractTests.EveryContract_ResolvesWithExactSignature` |
| Random isolation | Distribution runs inside the vanilla `GenerateOres` postfix, synchronous within the isolated terrain coroutine | `WorldGenRandomIsolation` + `WorldGenerationGenerateOresPatch` |
| No Abstractions leak | New public types are plain DTOs/enums; game/Unity types stay in GameAdapter | full build |

## 4. Verification results

| Evidence | Result |
|---|---|
| `dotnet build CasualtiesUnknownOnline.slnx` | 0 warnings / 0 errors |
| `dotnet test CasualtiesUnknownOnline.slnx` | 2077 passed / 0 failed |
| `dotnet format CasualtiesUnknownOnline.slnx` | clean |
| `tools/check-architecture.ps1` | pass |
| `tools/check-event-replay.ps1` | pass |
| `tools/check-entity-event-dispatch.ps1` | pass |
| `tools/check-delivery.ps1` | pass |
