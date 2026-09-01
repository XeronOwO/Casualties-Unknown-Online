# Mod recipe content binding — mechanism inventory and self-check

Owner cycle: CUCoreLib migration support — second concrete typed content kind
(recipe) after item.

Decision: expose recipes through the same `IModContent` + content-binder +
GameAdapter-provider seam. Mods keep a plain Abstractions DTO; the Game Adapter
builds vanilla `Recipe` objects only after the game recipe table exists, and
injects them once per table generation.

## 1. Mechanism inventory

| # | Mechanism | Evidence / decision |
|---|---|---|
| 1 | Typed recipe payload | `ModRecipeDefinition` in `CUO.Abstractions` with `ToPayload()`/`FromPayload()` via DataContractSerializer (no game/Unity type). |
| 2 | Ingredient DTO | `ModRecipeIngredient` — specific item/liquid id or crafting quality, minimum condition, destroy flag. |
| 3 | Category vocabulary | `ModRecipeCategory` plain string constants for the vanilla recipe categories. |
| 4 | Game Adapter provider | `GameAdapterRecipeContentProvider` decodes `ModRecipeDefinition`, waits for `Recipes.recipes`, builds game `Recipe` objects, and injects them. |
| 5 | Rebuild safety | The provider remembers the current recipe-table list; when a new game/layer rebuilds the table it clears injected keys and re-injects. |
| 6 | Duplicate safety | A recipe key (result + ingredient signature) is checked against the current table before adding; accepted definitions are not re-added. |
| 7 | Shared-content filter | Existing `ModContentBinder` applies the same network-mode filter for recipe content. |

## 2. Whole-family audit

| Family member | Change |
|---|---|
| `ModRecipeDefinition` | New Abstractions DTO + serialization helpers. |
| `ModRecipeIngredient` | New Abstractions ingredient DTO. |
| `ModRecipeCategory` | New plain category constants. |
| `GameAdapterRecipeContentProvider` | New GameAdapter provider routing to `Recipes.recipes`. |
| `PluginDependencyRegistrar` | Registered the recipe provider as `IContentBindingProvider` and `ICuoService`. |
| Tests | `ModRecipeDefinitionTests` round-trip and invalid payload. |
| Protocol version | Unchanged (no wire). |

## 3. Self-check table

| Mechanism | Change | Evidence |
|---|---|---|
| DTO round-trip | `ModRecipeDefinition.ToPayload`/`FromPayload` preserves result/ingredients/category | `ModRecipeDefinitionTests.RoundTrip_PreservesRecipeFields` |
| Invalid payload | Malformed bytes return null | `ModRecipeDefinitionTests.InvalidPayload_ReturnsNull` |
| Binder routing | Recipe entries can reach a provider through the generic binder | `ModContentBinderTests` (kind-routing provider test family) |
| No wire/protocol regression | Static recipes remain local; no NetMsg | `docs/api/mod-api.md` §4f, full suite green |

## 4. Verification design

- Pure-managed unit tests for DTO serialization and invalid payloads.
- GameAdapter recipe provider stays behind the same compile boundary as the
  item provider; its patch/DI wiring is verified through build and the
  existing GameAdapter contract tests.
- Static evidence: no game/Unity type in Abstractions; no new wire message.

## 5. Verification results

| Evidence | Result |
|---|---|
| `dotnet build CasualtiesUnknownOnline.slnx` | 0 warnings / 0 errors |
| `dotnet test CasualtiesUnknownOnline.slnx` | 1993 passed / 0 failed |
| `dotnet format CasualtiesUnknownOnline.slnx` | clean |
| `tools/check-architecture.ps1` | pass |
| `tools/check-event-replay.ps1` | pass |
| `tools/check-entity-event-dispatch.ps1` | pass |
| `tools/check-delivery.ps1` | pass |
