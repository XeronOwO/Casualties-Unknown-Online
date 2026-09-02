# Mod tile content binding — mechanism inventory and self-check

Owner cycle: CUCoreLib migration support — fifth concrete typed content kind
(tile) after item, recipe, liquid, and building.

Decision: expose static terrain-tile data through the same `IModContent` +
content-binder + GameAdapter-provider seam used by the earlier kinds. Mods keep
a plain Abstractions DTO; the Game Adapter maps static fields into the vanilla
world tile palette and `BlockInfo` lookup. World-generation placement and
runtime drop behavior are deliberately not part of this initial seam.

## 1. Mechanism inventory

| # | Mechanism | Evidence / decision |
|---|---|---|
| 1 | Typed tile payload | `ModTileDefinition` in `CUO.Abstractions` with `ToPayload()`/`FromPayload()` via DataContractSerializer. |
| 2 | Sleep-quality vocabulary | `ModTileSleepQuality` — plain enum mapping to the vanilla `Body.SleepQuality`-style four states. |
| 3 | Collider vocabulary | `ModTileColliderType` — plain enum mapping to the Unity `Tile.ColliderType` values (None/Sprite/Grid). |
| 4 | Visual source seam | `SpritePath` (resource path, future mod-local asset injection) plus `TemplateTileIndex` fallback that reuses a vanilla tile's sprite. |
| 5 | Game Adapter provider | `GameAdapterTileContentProvider` decodes `ModTileDefinition`, waits for `WorldGeneration.world`/`tiles`, allocates a deterministic custom block index at 36+, and injects a Unity `Tile`. |
| 6 | Tile factory | `CustomTileFactory` builds a `Tile` asset (sprite, color, collider type, name) without exposing Unity types to mods. |
| 7 | BlockInfo patch | `WorldGenerationGetBlockInfoPatch` prefixes the vanilla `GetBlockInfo` switch; custom indices get the provider-built `BlockInfo`, vanilla indices keep native behavior. |
| 8 | Locale support | Display/description text is applied to `Locale.currentLang.other` (the same dictionary used for vanilla tile names). |
| 9 | Duplicate/index safety | Duplicate content ids are refused; custom indices never overlap vanilla indices. |
| 10 | Shared-content filter | Existing `ModContentBinder` applies the same network-mode filter as all other static content kinds. |

## 2. Whole-family audit

| Family member | Change |
|---|---|
| `ModTileDefinition` | New Abstractions DTO + serialization helpers. |
| `ModTileSleepQuality` | New Abstractions enum. |
| `ModTileColliderType` | New Abstractions enum. |
| `GameAdapterTileContentProvider` | New GameAdapter provider mapping into `WorldGeneration.tiles` and custom `BlockInfo`. |
| `CustomTileFactory` | New GameAdapter tile-asset construction helper. |
| `WorldGenerationGetBlockInfoPatch` | New Harmony prefix for custom tile `BlockInfo`. |
| `IPatchBridge` / `GameAdapterBridge` | Added `TryGetCustomBlockInfo` so the static patch reaches the state-owning provider. |
| `GameAdapterDomains` / `GameAdapter` | Wired the tile provider into the adapter's owned domain set. |
| `PluginDependencyRegistrar` | Registered the tile provider as `IContentBindingProvider` and `ICuoService`. |
| Tests | `ModTileDefinitionTests` round-trip and invalid payload. |
| Protocol version | Unchanged (no wire). |

## 3. Self-check table

| Mechanism | Change | Evidence |
|---|---|---|
| DTO round-trip | `ModTileDefinition.ToPayload`/`FromPayload` preserves static fields and enums | `ModTileDefinitionTests.RoundTrip_PreservesCoreFields` |
| Optional visual source | Null template index and empty sprite path survive the round-trip | `ModTileDefinitionTests.RoundTrip_PreservesMissingOptionalVisualSource` |
| Invalid payload | Malformed bytes return null | `ModTileDefinitionTests.InvalidPayload_ReturnsNull` |
| Patch contract | The new `WorldGeneration.GetBlockInfo` prefix resolves against the real game method | `PatchContractTests.EveryContract_ResolvesWithExactSignature` |
| Binder routing | Tile entries can reach a provider through the generic binder | `ModContentBinderTests` (kind-routing provider test family) |
| No wire/protocol regression | Static tiles remain local; no NetMsg | `docs/api/mod-api.md` §4f, full suite green |

## 4. Verification design

- Pure-managed unit tests for DTO serialization and invalid payloads.
- GameAdapter tile provider/factory stay behind the same compile boundary as
  the item/recipe/liquid/building providers; DI wiring is verified through the
  full solution build and the existing generic binder contract tests.
- The new Harmony patch is covered automatically by `PatchInventory` /
  `PatchContractTests`, which resolve every attributed patch against the real
  game assembly before launch.
- Static evidence: no game/Unity type in Abstractions; no new wire message;
  no random world-gen or drop behavior added in this seam.

## 5. Verification results

| Evidence | Result |
|---|---|
| `dotnet build CasualtiesUnknownOnline.slnx` | 0 warnings / 0 errors |
| `dotnet test CasualtiesUnknownOnline.slnx` | 2001 passed / 0 failed |
| `dotnet format CasualtiesUnknownOnline.slnx` | clean |
| `tools/check-architecture.ps1` | pass |
| `tools/check-event-replay.ps1` | pass (33 events) |
| `tools/check-entity-event-dispatch.ps1` | pass (33 kinds x 3 tables) |
| `tools/check-delivery.ps1` | pass (7 boxes) |
