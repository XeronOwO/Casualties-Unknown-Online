# Mod item explicit fixed drop sources — mechanism inventory and self-check

Owner cycle: CUCoreLib migration support — explicit fixed drop-source pools.

Decision: add the CUCoreLib `DropPool` concept to the existing item content seam
without porting its intrusive full-method overrides or its JToken snapshot
channel. A mod can now choose explicit vanilla loot containers for a custom
item through `ModItemDefinition.DropSources`; the Game Adapter represents each
source as a synthetic `ItemLootPool` category and lets the existing vanilla
corpse/crate/trader loot code consume it. No new wire message and no game/Unity
type crosses Abstractions.

## 1. Mechanism inventory

| # | Mechanism | Evidence / decision |
|---|---|---|
| 1 | Typed payload field | `ModItemDefinition.DropSources` (`ModItemDropSource?`) in Abstractions; the enum is a plain `[Flags]` vocabulary with the same source set as CUCoreLib's `DropPool` and the DTO remains plain data. |
| 2 | Provider validation | `GameAdapterItemContentProvider` accepts the field as-is; the existing `WorldSpawnPerChunk` validation remains separate. Drop sources use `SpawnFrequency` as the per-source weight, matching CUCoreLib's fixed-source registration. |
| 3 | Source pool registration | The provider creates stable synthetic `ItemLootPool` categories (`cuo_drop_corpse`, `cuo_drop_medical_crate`, etc.) and adds each bound item `SpawnFrequency` times per selected source. Composite flags (`AllTraders`) expand to the individual trader sources. |
| 4 | Generic-category suppression | If `DropSources` is non-null, the item is not added to its vanilla category; if the game rebuilds the pool from `Item.GlobalItems`, the provider removes the generic-category entry so the authored source selection stays authoritative. |
| 5 | Corpse seam | `CorpseScript.Start` prefix appends the corpse source category to the corpse's category list, so the vanilla corpse loot rolls can select explicit custom items. |
| 6 | Building crate seam | `BuildingEntity.Start` prefix maps built-in crate/building ids (`medcrate`, `foodbox`, `containercrate`, `lifepodchest`, `dropcapsule`) to the matching source and appends that category to `itemCategoriesToAdd`. |
| 7 | Trader seam | Host-side `TraderScript.GenerateSingleItemList` prefix replaces the stock-generation method only when explicit trader sources exist; it replicates the vanilla formula and adds the active trader source categories to the category list. Guests never generate stock (existing `TraderStartPatch`), so the stock remains host-authoritative. |
| 8 | Patch boundary | New `ModItemDropSourcePatches` reads only `IPatchBridge.TryGetModDropSourceCategory`; `GameAdapterBridge` forwards to the item content provider. |
| 9 | Sync | No new wire: corpse/crate loot is part of the existing local/generated item flow, and trader stock already travels through the existing trade state channel. |
| 10 | Non-goals | CUCoreLib's asset-backed visual modes, advanced item behaviours, and its full trader UI override remain out of scope. |

## 2. Whole-family audit

| Family member | Change |
|---|---|
| `ModItemDropSource` | New Abstractions flags enum (corpse, crates, traders, drop capsule, capsule container). |
| `ModItemDefinition` | Added nullable `DropSources` DTO field. |
| `GameAdapterItemContentProvider` | Added fixed-source synthetic pool registration, generic-category suppression/removal, and `TryGetDropSourceCategory`. |
| `ModItemDropSourcePatches` | New GameAdapter patch file for corpse/building/trader source category hooks. |
| `IPatchBridge` / `GameAdapterBridge` | Added `TryGetModDropSourceCategory` forwarding. |
| Tests | `ModItemDefinitionTests`, `ItemDropSourceProviderTests`, `PatchContractTests`. |

## 3. Self-check table

| Mechanism | Change | Evidence |
|---|---|---|
| DTO round-trip | `DropSources` survives `ToPayload`/`FromPayload` | `ModItemDefinitionTests.RoundTrip_PreservesCoreFields` |
| Source pool registration | Explicit sources seed stable synthetic `ItemLootPool` categories with frequency weights | `ItemDropSourceProviderTests.Update_ExplicitDropSourceSuppressesCategoryFallbackAndSeedsSourcePool` |
| Composite expansion | `AllTraders` expands to trader 1/2/3 categories | `ItemDropSourceProviderTests.Update_AllTradersExpandsToIndividualTraderCategories` |
| Generic suppression | An item with fixed sources is removed from its vanilla category even after a pool rebuild | `ItemDropSourceProviderTests.Update_ExplicitSourcesRemoveGenericEntryFromRebuiltPool` |
| Zero-frequency boundary | No source pool is registered when `SpawnFrequency` is zero | `ItemDropSourceProviderTests.Update_ZeroFrequencyRegistersNoExplicitSourcePool` |
| Patch contract | New corpse/building/trader Harmony patches resolve against the real game assembly | `PatchContractTests` |
| No wire/protocol regression | Static content and loot routing use existing seams; no NetMsg added | `docs/api/mod-api.md` §4f, full suite green |

## 4. Verification results

| Evidence | Result |
|---|---|
| `dotnet build CasualtiesUnknownOnline.slnx` | 0 warnings / 0 errors |
| `dotnet test CasualtiesUnknownOnline.slnx` | 2101 passed / 0 failed |
| `dotnet format CasualtiesUnknownOnline.slnx` | see final gate result |
| `tools/check-architecture.ps1` | pass |
| `tools/check-event-replay.ps1` | pass |
| `tools/check-entity-event-dispatch.ps1` | pass |
| `tools/check-delivery.ps1` | pass |
