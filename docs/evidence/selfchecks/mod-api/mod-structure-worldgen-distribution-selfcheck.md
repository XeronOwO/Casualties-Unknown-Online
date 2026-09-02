# Mod structure worldgen distribution — mechanism inventory and self-check

Owner cycle: CUCoreLib migration support — automatic world-generation placement
for mod-authored multi-block structures after the typed structure content and
runtime placement seams landed.

Decision: consume the per-depth `SpawnCounts` already carried by
`ModStructureDefinition` during CUO's own world generation. The distribution
runs as a coroutine wrapper around the vanilla `WorldGenerateWorldBorders`
iterator, so `WorldGenRandomIsolation` drives it on the sealed generation
stream; both host and guest see the same placements without any wire message.
Only the compiled static block grid is placed — no entity, loot, liquid,
background, or custom-data layer.

## 1. Mechanism inventory

| # | Mechanism | Evidence / decision |
|---|---|---|
| 1 | Spawn-count lookup | `ModStructureDefinition.TryGetSpawnCount(depth, out count)` resolves a per-depth count and clamps negative values to zero. |
| 2 | Deterministic enumeration | `GameAdapterStructureContentProvider.GetCompiledForWorldGen()` snapshots accepted structures ordered by id, so both sides consume the shared Random stream in the same order. |
| 3 | Generation hook | `WorldGenerationStructureDistributionPatch` postfix wraps the vanilla `WorldGenerateWorldBorders` iterator. This point is after vanilla terrain/structure generation and before the collider/UpdateWorld pass (the same point CUCoreLib uses). |
| 4 | Random isolation | The wrapper is driven by `WorldGenRandomIsolation.Wrap` as a nested coroutine; all `UnityEngine.Random` consumption lands inside the sealed generation stream. |
| 5 | Placement boundary | `StructureWorldGenDistribution` chooses candidate origins, validates the full structure against world bounds, resolves custom tile content ids, then calls `WorldGeneration.SetBlock` per non-air cell. |
| 6 | No wire | `WorldGenerationSetBlockPatch` routes to `WorldEventSync.OnBlockSet`, which returns immediately while `HarmonyTraverse.IsGenerating()` is true. Generation writes are the baseline, not replicated mutations. |
| 7 | Static only | The DTO has no entity/loot/background layer in this seam; only compiled block cells are written. |
| 8 | Tutorial guard | Distribution is skipped when `world.biomeOverride == Tutorial`; normal depths use the indexed spawn count. |
| 9 | Safety limit | Candidate centers are at least 50 blocks from world edges, max 24 placement attempts per copy, per-structure missing-custom-tile failure reported once. |

## 2. Whole-family audit

| Family member | Change |
|---|---|
| `ModStructureDefinition` | Added `TryGetSpawnCount` typed helper for worldgen/consumers. |
| `GameAdapterStructureContentProvider` | Added `GetCompiledForWorldGen` stable-id snapshot. |
| `StructureWorldGenDistribution` | New GameAdapter domain: coroutine wrapper + placement loop. |
| `WorldGenerationStructureDistributionPatch` | New Harmony patch wrapping `WorldGenerateWorldBorders`. |
| `IPatchBridge` / `GameAdapterBridge` | Added `WrapStructureWorldGen` forwarding seam. |
| `GameAdapterDomains` | Owns `StructureWorldGenDistribution`. |
| Tests | `ModStructureDefinitionTests.TryGetSpawnCount_*` + reflective `StructureWorldGenProviderTests.GetCompiledForWorldGen_ReturnsStableIdOrder`. |
| Protocol version | Unchanged (no wire). |

## 3. Self-check table

| Mechanism | Change | Evidence |
|---|---|---|
| Spawn-count lookup | Depth-indexed count, missing/negative handling | `ModStructureDefinitionTests.TryGetSpawnCount_*` |
| Deterministic order | Provider snapshot is id-ordered | `StructureWorldGenProviderTests.GetCompiledForWorldGen_ReturnsStableIdOrder` |
| Patch exists/resolves | New `WorldGenerateWorldBorders` contract participates in the existing patch inventory | `PatchContractTests.EveryContract_ResolvesWithExactSignature` / `Contracts_CoverEveryAttributedPatchClass_PlusTheDynamicOnes` |
| No wire | Generation-time `SetBlock` already filtered by `WorldEventSync.OnBlockSet`'s `IsGenerating` guard | `WorldGenerationSetBlockPatch` / `WorldEventSync.OnBlockSet` |
| Random isolation | Wrapper runs as a nested `IEnumerator` under `WorldGenRandomIsolation.Drive` | `WorldGenRandomIsolation` + `WorldGenerationStructureDistributionPatch` |
| No Abstractions leak | New code stays in GameAdapter/Abstractions helper (no game/Unity type in public API) | `docs/api/mod-api.md` + full build |

## 4. Verification design

- Pure-managed tests for `TryGetSpawnCount` and reflective GameAdapter provider
  snapshot ordering (no Unity world needed).
- The actual world writes remain behind the GameAdapter compile boundary and
  are governed by the existing patch-contract tests plus the generation-time
  SetBlock guard; no new wire path is introduced.
- Static evidence: no new permission bit, no new NetMsg, no Abstractions
  game/Unity type, no CUCoreLib source committed.

## 5. Verification results

| Evidence | Result |
|---|---|
| `dotnet build CasualtiesUnknownOnline.slnx` | 0 warnings / 0 errors |
| `dotnet test CasualtiesUnknownOnline.slnx` | 2023 passed / 0 failed |
| `dotnet format CasualtiesUnknownOnline.slnx` | clean |
| `tools/check-architecture.ps1` | pass |
| `tools/check-event-replay.ps1` | pass |
| `tools/check-entity-event-dispatch.ps1` | pass |
| `tools/check-delivery.ps1` | pass |
