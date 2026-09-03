# Mod item visual — mechanism inventory and self-check

Owner cycle: CUCoreLib migration support — the worn-sprite, multi-limb
additive-sprite and liquid-mask visual slice on top of the typed custom item
behavior seam.

Decision: extend the plain `ModItemDefinition` DTO with a `ModItemVisual`
object carrying resource-path sprites and local offsets, plus a
`ModItemLimbWornSprite` list for additive per-limb worn visuals. The Game
Adapter resolves those paths on the cached inactive runtime template and stores
the base/worn/liquid/multi-limb sprites on a per-instance
`CustomItemVisualState` component. Thin Harmony hooks apply/restore the worn
sprite on wear/drop, configure and present the vanilla `Wearable` secondary
sprites, and re-apply the liquid fill sprite after
`WaterContainerItem.Start`. Unity sprites never cross Abstractions; no new wire
message is added.

## 1. Mechanism inventory

| # | Mechanism | Evidence / decision |
|---|---|---|
| 1 | Typed visual DTO | `ModItemVisual` is a plain DataContract type in Abstractions with `WornSpritePath`, worn offsets/sorting order, `LiquidMaskPath`, and `MultiWornSprites`; each entry is a `ModItemLimbWornSprite` (limb name + sprite path + local offsets). |
| 2 | Definition wiring | `ModItemDefinition.Visual` is optional and round-trips through the existing `ToPayload`/`FromPayload` contract, including the multi-limb list. |
| 3 | Provider validation | `CustomItemBehaviorValidator` refuses NaN/Infinity worn-sprite and multi-limb offsets before the definition is accepted. |
| 4 | Runtime visual state | `CustomItemVisualState` captures the template's normal sprite/sorting order and stores resolved worn/liquid/multi-limb sprites plus offsets. |
| 5 | Template application | `CustomItemBehaviorApplier.ApplyVisual` loads the resource paths, attaches/marks the visual state on the cached template, adds `Wearable` when multi-limb sprites are authored, seeds the wearable arrays, and applies the liquid fill sprite. |
| 6 | Wear/drop hooks | `CustomItemVisualPatches` applies the worn visual after a successful `Body.WearWearable` parent-to-limb result and restores the normal visual after `Body.DropWearable`; a `Wearable.CreateSprites` prefix/postfix pair configures and presents the additive limb sprites. |
| 7 | Remote restore/clone path | `CharacterDataSync.RestoreWearable` and `CloneInventoryRenderer.RenderItemInto` apply the same worn visual and materialize additive limb sprites because reconnect and remote display restores never run the vanilla wear flow. |
| 8 | Liquid mask | `WaterContainerItem.Start` postfix re-applies the mask from the per-instance state after the native start initializer. |
| 9 | Non-goals | Animated sprites, CUCoreLib asset loading, asset-backed visual modes, and custom-data runtime bags remain future. |

## 2. Whole-family audit

| Family member | Change |
|---|---|
| `ModItemLimbWornSprite` | New Abstractions DataContract DTO for one additive limb sprite assignment. |
| `ModItemVisual` | New Abstractions DataContract DTO with primary worn, liquid-mask and multi-limb visual data. |
| `ModItemDefinition` | Added optional `Visual` property. |
| `CustomItemVisualState` | New GameAdapter per-instance visual component; stores and applies worn/liquid/multi-limb visual state. |
| `CustomItemBehaviorValidator` | Added NaN/Infinity visual-offset validation, including multi-limb entries. |
| `CustomItemBehaviorApplier` | Added visual resource resolution/application on the runtime template, including `Wearable` secondary-array seeding. |
| `CustomItemVisualPatches` | New Harmony file for wear/drop/liquid-mask visual hooks and `Wearable.CreateSprites` multi-limb configuration/presentation. |
| `CharacterDataSync` | Restored worn items now apply the custom worn visual and materialize additive limb sprites. |
| `CloneInventoryRenderer` | Remote worn display proxies now apply the custom worn visual and materialize additive limb sprites. |
| Tests | `ModItemDefinitionTests.RoundTrip_PreservesVisualFields`, `ItemAdvancedBehaviorProviderTests.TryBind_AcceptsVisualDto`, `TryBind_RejectsInvalidAdvancedBehaviorValues` (multi-limb branch). |
| Protocol version | Unchanged (no wire). |

## 3. Self-check table

| Mechanism | Change | Evidence |
|---|---|---|
| DTO round-trip | `Visual` fields including `MultiWornSprites` survive `ToPayload`/`FromPayload` | `ModItemDefinitionTests.RoundTrip_PreservesVisualFields` |
| Provider acceptance/validation | A definition carrying `Visual` including multi-limb entries is accepted; NaN/Infinity worn and multi-limb offsets are refused | `ItemAdvancedBehaviorProviderTests.TryBind_AcceptsVisualDto`, `TryBind_RejectsInvalidAdvancedBehaviorValues` |
| Template path | The behavior applier runs after template rename and before mod `SpawnComponents`, resolves multi-limb resource sprites, seeds `Wearable` arrays and the visual state | `CustomItemTemplateFactory.Create` calls `CustomItemBehaviorApplier.Apply` |
| Wear/drop behavior | The worn sprite is applied only after a successful limb parent and restored on drop; `Wearable.CreateSprites` prefix/postfix configures and presents additive limb sprites | `CustomItemVisualPatches` |
| Remote/restore behavior | `RestoreWearable` and remote clone rendering apply the worn visual and materialize additive limb sprites without running the vanilla wear flow | `CharacterDataSync.RestoreWearable`, `CloneInventoryRenderer.RenderItemInto` |
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
