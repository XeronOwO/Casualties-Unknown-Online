# Mod item advanced behavior — mechanism inventory and self-check

Owner cycle: CUCoreLib migration support — the minimal usable container,
battery, light, tool and gun behavior slice on top of the typed item content
seam.

Decision: extend the plain `ModItemDefinition` DTO with five behavior DTOs and
map them entirely in the GameAdapter. Tool/gun/battery shape the static
`ItemInfo` surface at injection time; container/battery/gun/light configure
the inactive runtime item template before a spawned clone runs Awake/Start.
Game delegates and Unity types never cross Abstractions; no new wire message
is added.

## 1. Mechanism inventory

| # | Mechanism | Evidence / decision |
|---|---|---|
| 1 | Typed behavior DTOs | `ModItemContainer`, `ModItemBattery`, `ModItemLight`, `ModItemTool`, `ModItemGun` plus their stable enums are plain DataContract types in Abstractions. |
| 2 | Provider validation | `CustomItemBehaviorValidator` refuses negative container/tool/light values, NaN battery start charge, non-0..1 light colors, negative gun fields, negative magazine capacity, and zero shots per fire before the definition is accepted. |
| 3 | Static ItemInfo mapping | `GameAdapterItemContentProvider.BuildItemInfo` wires tool/gun `useAction`, `usable`, `usableWithLMB`, `autoAttack`, the `gun` tag, battery `destroyAtZeroCondition` / `decayInfo` defaults, and `DecayMinutes` → `rotSpeed`. |
| 4 | Runtime component mapping | `CustomItemBehaviorApplier` configures vanilla `Container`, `BatteryItem`, and `GunScript` on the cached runtime template, and creates/configured `Light2D` through the existing reflection-by-name convention for URP. |
| 5 | Template path | `CustomItemTemplateFactory` calls the behavior applier after renaming the template and before mod-authored `SpawnComponents`, so all custom item materialization paths inherit the behavior. |
| 6 | Battery defaults | Preset-to-capacity/type mapping follows vanilla small/medium/large (50/100/300); `StartCharge` supports percentage/fraction or absolute and a negative sentinel for full. |
| 7 | Gun overrides | Nullable vanilla `GunScript` fields keep the base prefab defaults unless the mod explicitly overrides them; sprite fallbacks copy the template `SpriteRenderer` when the component has none. |
| 8 | Non-goals | Worn sprites, liquid-mask visuals, animate sprites, custom-data runtime bag, and asset-backed sprite modes remain future. |

## 2. Whole-family audit

| Family member | Change |
|---|---|
| `ModItemDefinition` | Added `Container`, `Battery`, `Light`, `Tool`, `Gun` optional behavior properties. |
| `ModItemContainer` / `ModItemBattery` / `ModItemLight` / `ModItemTool` / `ModItemGun` | New Abstractions DataContract DTOs. |
| `ModBatteryPreset` / `ModLightType` / `ModGunAmmoType` / `ModGunFiringMode` / `ModGunFeedType` | New Abstractions enums mirroring vanilla/URP values. |
| `CustomItemBehaviorValidator` | New GameAdapter validation seam. |
| `CustomItemBehaviorApplier` | New GameAdapter runtime template mapping seam (light via reflection). |
| `GameAdapterItemContentProvider` | Added advanced validation, tool/gun/battery static mapping, and reflection-safe tag population. |
| `CustomItemTemplateFactory` | Invokes the behavior applier before `CustomComponentAttach`. |
| Tests | `ModItemDefinitionTests`, `ItemAdvancedBehaviorProviderTests`. |
| Protocol version | Unchanged (no wire). |

## 3. Self-check table

| Mechanism | Change | Evidence |
|---|---|---|
| DTO round-trip | All five behavior DTOs and their enums survive `ToPayload`/`FromPayload` | `ModItemDefinitionTests.RoundTrip_PreservesAdvancedBehaviorFields` |
| Static tool mapping | Tool DTO forces `usable` / `usableWithLMB` / `autoAttack` and installs a non-null `useAction` | `ItemAdvancedBehaviorProviderTests.Update_ToolAndGunSetStaticUseDefaultsAndTags` |
| Static gun mapping | Gun DTO forces the same use flags, adds the `gun` tag, and installs a trigger `useAction` | same test |
| Battery static mapping | Battery overrides `destroyAtZeroCondition` to false, sets `decayInfo & 16`, and `DecayMinutes` populates `decayMinutes` / `rotSpeed` | `ItemAdvancedBehaviorProviderTests.Update_BatteryOverridesDestroyAtZeroAndSetsDecayFlag` |
| Validation | Negative container/battery NaN/light/tool/gun values and bad mag/shots are refused | `ItemAdvancedBehaviorProviderTests.TryBind_RejectsInvalidAdvancedBehaviorValues` |
| No wire/protocol regression | Static content and runtime template behavior still use existing seams; no NetMsg added | `docs/api/mod-api.md` §4f, full suite green |

## 4. Verification design

- Pure-managed tests cover DTO round-trip and reflective GameAdapter provider
  static mapping/validation, matching the existing item-provider contract-test
  style (test project never compile-references GameAdapter).
- The runtime `GameObject` component mapping itself is exercised by the
  existing GameAdapter template path and the game-assembly contract tests; no
  new wire path or Unity type in Abstractions is introduced.
- Static evidence: no new permission bit, no new NetMsg, no game/Unity type in
  Abstractions, no CUCoreLib source committed.

## 5. Verification results

| Evidence | Result |
|---|---|
| `dotnet build CasualtiesUnknownOnline.slnx` | 0 warnings / 0 errors |
| `dotnet test CasualtiesUnknownOnline.slnx` | 2110 passed / 0 failed |
| `dotnet format CasualtiesUnknownOnline.slnx` | clean |
| `tools/check-architecture.ps1` | pass |
| `tools/check-event-replay.ps1` | pass |
| `tools/check-entity-event-dispatch.ps1` | pass |
| `tools/check-delivery.ps1` | pass |
