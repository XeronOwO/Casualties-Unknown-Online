# Mod item visual — mechanism inventory and self-check

Owner cycle: CUCoreLib migration support — the basic worn-sprite and
liquid-mask visual slice on top of the typed custom item behavior seam.

Decision: extend the plain `ModItemDefinition` DTO with a `ModItemVisual`
object carrying resource-path sprites and local offsets. The Game Adapter
resolves those paths on the cached inactive runtime template and stores the
base/worn/liquid sprites on a per-instance `CustomItemVisualState` component.
Thin Harmony hooks apply/restore the worn sprite on wear/drop and re-apply the
liquid fill sprite after `WaterContainerItem.Start`. Unity sprites never cross
Abstractions; no new wire message is added.

## 1. Mechanism inventory

| # | Mechanism | Evidence / decision |
|---|---|---|
| 1 | Typed visual DTO | `ModItemVisual` is a plain DataContract type in Abstractions with `WornSpritePath`, worn offsets/sorting order, and `LiquidMaskPath`. |
| 2 | Definition wiring | `ModItemDefinition.Visual` is optional and round-trips through the existing `ToPayload`/`FromPayload` contract. |
| 3 | Provider validation | `CustomItemBehaviorValidator` refuses NaN/Infinity worn-sprite offsets before the definition is accepted. |
| 4 | Runtime visual state | `CustomItemVisualState` captures the template's normal sprite/sorting order and stores resolved worn/liquid sprites plus offsets. |
| 5 | Template application | `CustomItemBehaviorApplier.ApplyVisual` loads the resource paths, attaches/marks the visual state on the cached template, and seeds the liquid fill sprite. |
| 6 | Wear/drop hooks | `CustomItemVisualPatches` applies the worn visual after a successful `Body.WearWearable` parent-to-limb result and restores the normal visual after `Body.DropWearable`. |
| 7 | Remote restore/clone path | `CharacterDataSync.RestoreWearable` and `CloneInventoryRenderer.RenderItemInto` apply the same worn visual because reconnect and remote display restores never run the vanilla wear flow. |
| 8 | Liquid mask | `WaterContainerItem.Start` postfix re-applies the mask from the per-instance state after the native start initializer. |
| 9 | Non-goals | Multi-limb worn sprites, animated sprites, CUCoreLib asset loading, and custom-data runtime bags remain future. |

## 2. Whole-family audit

| Family member | Change |
|---|---|
| `ModItemVisual` | New Abstractions DataContract DTO. |
| `ModItemDefinition` | Added optional `Visual` property. |
| `CustomItemVisualState` | New GameAdapter per-instance visual component. |
| `CustomItemBehaviorValidator` | Added NaN/Infinity visual-offset validation. |
| `CustomItemBehaviorApplier` | Added visual resource resolution/application on the runtime template. |
| `CustomItemVisualPatches` | New Harmony file for wear/drop/liquid-mask visual hooks. |
| `CharacterDataSync` | Restored worn items now apply the custom worn visual. |
| `CloneInventoryRenderer` | Remote worn display proxies now apply the custom worn visual. |
| Tests | `ModItemDefinitionTests.RoundTrip_PreservesVisualFields`, `ItemAdvancedBehaviorProviderTests.TryBind_AcceptsVisualDto`. |
| Protocol version | Unchanged (no wire). |

## 3. Self-check table

| Mechanism | Change | Evidence |
|---|---|---|
| DTO round-trip | `Visual` fields survive `ToPayload`/`FromPayload` | `ModItemDefinitionTests.RoundTrip_PreservesVisualFields` |
| Provider acceptance/validation | A definition carrying `Visual` is accepted; NaN/Infinity worn offsets are refused | `ItemAdvancedBehaviorProviderTests.TryBind_AcceptsVisualDto`, `TryBind_RejectsInvalidAdvancedBehaviorValues` |
| Template path | The behavior applier runs after template rename and before mod `SpawnComponents`, so every custom item materialization inherits the visual state | `CustomItemTemplateFactory.Create` calls `CustomItemBehaviorApplier.Apply` |
| Wear/drop behavior | The worn sprite is applied only after a successful limb parent and restored on drop | `CustomItemVisualPatches` |
| Remote/restore behavior | `RestoreWearable` and remote clone rendering apply the worn visual without running the vanilla wear flow | `CharacterDataSync.RestoreWearable`, `CloneInventoryRenderer.RenderItemInto` |
| No wire/protocol regression | No NetMsg, no JObject snapshot, no generic channel | `docs/api/mod-api.md` §4f, full suite green |

## 4. Verification design

- Pure-managed tests cover DTO round-trip and provider acceptance in the
  existing reflective contract-test style (test project never
  compile-references GameAdapter).
- The actual Unity component/hook behavior is exercised by the GameAdapter
  template path and the game-assembly contract tests; no new wire path or Unity
  type in Abstractions is introduced.
- Static evidence: no new permission bit, no new NetMsg, no game/Unity type in
  Abstractions, no CUCoreLib source committed.

## 5. Verification results

| Evidence | Result |
|---|---|
| `dotnet build CasualtiesUnknownOnline.slnx` | 0 warnings / 0 errors |
| `dotnet test CasualtiesUnknownOnline.slnx` | 2112 passed / 0 failed |
| `dotnet format CasualtiesUnknownOnline.slnx` | clean |
| `tools/check-architecture.ps1` | pass |
| `tools/check-event-replay.ps1` | pass |
| `tools/check-entity-event-dispatch.ps1` | pass |
| `tools/check-delivery.ps1` | pass |
