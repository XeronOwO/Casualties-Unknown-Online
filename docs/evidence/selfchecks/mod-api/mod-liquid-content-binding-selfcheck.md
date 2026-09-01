# Mod liquid content binding — mechanism inventory and self-check

Owner cycle: CUCoreLib migration support — third concrete typed content kind
(liquid) after item and recipe.

Decision: expose static liquid data through the same `IModContent` +
content-binder + GameAdapter-provider seam. Mods keep a plain Abstractions DTO;
the Game Adapter maps static fields into the vanilla `Liquids.Registry` and
applies local locale entries. Game delegates are intentionally not part of the
DTO because mods must not pass game delegate types through Abstractions.

## 1. Mechanism inventory

| # | Mechanism | Evidence / decision |
|---|---|---|
| 1 | Typed liquid payload | `ModLiquidDefinition` in `CUO.Abstractions` with `ToPayload()`/`FromPayload()` via DataContractSerializer. |
| 2 | Quality tags | `ModLiquidQuality` plain DTO for crafting-quality id/amount. |
| 3 | Game Adapter provider | `GameAdapterLiquidContentProvider` decodes `ModLiquidDefinition`, waits for `Liquids.Registry`, and injects a `LiquidType`. |
| 4 | Locale support | Display/description text is added to the loaded `Locale.currentLang.other` dictionary (local presentation only). |
| 5 | Duplicate safety | Existing vanilla/custom liquids are never overwritten by the provider. |
| 6 | Shared-content filter | Existing `ModContentBinder` applies the same network-mode filter for liquid content. |

## 2. Whole-family audit

| Family member | Change |
|---|---|
| `ModLiquidDefinition` | New Abstractions DTO + serialization helpers. |
| `ModLiquidQuality` | New Abstractions quality DTO. |
| `GameAdapterLiquidContentProvider` | New GameAdapter provider mapping into `Liquids.Registry`. |
| `PluginDependencyRegistrar` | Registered the liquid provider as `IContentBindingProvider` and `ICuoService`. |
| Tests | `ModLiquidDefinitionTests` round-trip and invalid payload. |
| Protocol version | Unchanged (no wire). |

## 3. Self-check table

| Mechanism | Change | Evidence |
|---|---|---|
| DTO round-trip | `ModLiquidDefinition.ToPayload`/`FromPayload` preserves static fields and qualities | `ModLiquidDefinitionTests.RoundTrip_PreservesLiquidFields` |
| Invalid payload | Malformed bytes return null | `ModLiquidDefinitionTests.InvalidPayload_ReturnsNull` |
| Binder routing | Liquid entries can reach a provider through the generic binder | `ModContentBinderTests` (kind-routing provider test family) |
| No wire/protocol regression | Static liquids remain local; no NetMsg | `docs/api/mod-api.md` §4f, full suite green |

## 4. Verification design

- Pure-managed unit tests for DTO serialization and invalid payloads.
- GameAdapter liquid provider stays behind the same compile boundary as the
  item/recipe providers; DI wiring is verified through build.
- Static evidence: no game/Unity type in Abstractions; no game delegate in the
  DTO; no new wire message.

## 5. Verification results

| Evidence | Result |
|---|---|
| `dotnet build CasualtiesUnknownOnline.slnx` | 0 warnings / 0 errors |
| `dotnet test CasualtiesUnknownOnline.slnx` | 1995 passed / 0 failed |
| `dotnet format CasualtiesUnknownOnline.slnx` | clean |
| `tools/check-architecture.ps1` | pass |
| `tools/check-event-replay.ps1` | pass |
| `tools/check-entity-event-dispatch.ps1` | pass |
| `tools/check-delivery.ps1` | pass |
