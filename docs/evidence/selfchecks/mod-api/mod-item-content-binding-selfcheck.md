# Mod item content binding — mechanism inventory and self-check

Owner cycle: CUCoreLib migration support — first concrete content binding
kind (item) plus the generic content-binding skeleton.

Decision: implement typed static item definition registration through CUO's
opaque content registry. The mod-facing surface stays in Abstractions and
uses plain data; Runtime owns the generic binder; the Game Adapter owns the
only game-facing registration. The multi-mod data-sync boundary is handled
for static content by only binding shared-content network modes.

## 1. Mechanism inventory

| # | Mechanism | Evidence / decision |
|---|---|---|
| 1 | Typed item payload | `ModItemDefinition` in `CUO.Abstractions` with `ToPayload()`/`FromPayload()` via BCL DataContractSerializer (no new package, no game/Unity type). |
| 2 | Generic content binder | `IContentBindingProvider` + `ModContentBinder` in Runtime: routes opaque content entries by kind after first-frame mod discovery. |
| 3 | Shared-content filter | Binder only routes content from `NetworkMode.Synchronized`, `Authoritative`, or `RequiresAllPlayers`; `HostOnly` and local-only mods are skipped and logged. |
| 4 | Game Adapter item provider | `GameAdapterItemContentProvider` accepts `ModItemDefinition`, waits for `Item.GlobalItems`, injects a static `ItemInfo`, and builds an optional runtime template from `TemplateId`. |
| 5 | Runtime item template | `CustomItemTemplateFactory` clones the vanilla base prefab, renames it, attaches `SpawnComponents`, and caches it inactive; `ItemPrefabResolver` serves it to CUO's item materialization paths. |
| 6 | `Utils.Create` custom fallback | `UtilsCreateCustomItemPatch` prefixes both `Utils.Create` overloads and takes over only when the id has a custom template; vanilla/missing ids keep native behavior. |
| 7 | Native direct-resource fallback | `NativeItemResourcePatches` transpiles `BuildingEntity.Update` and `SaveSystem.TryLoadGame` to route `Resources.Load`/`Object.Instantiate` through `ItemPrefabResolver`, covering building death drops and vanilla save restore. |
| 8 | Runtime/permission path | Existing `IModContent` permission/version/opaque-byte rules still apply; content bytes never cross the wire. |
| 9 | DI wiring | `CuoBootstrap` registers the binder as an `ICuoService` after `ModService`; the plugin registers the Game Adapter provider and injects it into `GameAdapter`. |

## 2. Whole-family audit

| Family member | Change |
|---|---|
| `ModItemDefinition` | Abstractions DTO + serialization helpers; added `TemplateId` and `SpawnComponents`. |
| `IContentBindingProvider` | Runtime → Game Adapter boundary for one content kind. |
| `ModContentBinder` | Generic one-shot binder with kind/provider routing and shared-mode filtering. |
| `GameAdapterItemContentProvider` | GameAdapter provider registering `ItemInfo` and building/serving runtime item templates. |
| `CustomItemTemplateFactory` | GameAdapter template construction from a vanilla base prefab and mod component types. |
| `ItemPrefabResolver` | GameAdapter item-prefab resolution seam used by restore/spawn/render call sites. |
| `UtilsCreateCustomItemPatch` | Harmony prefix for both `Utils.Create` overloads to materialize custom templates. |
| `NativeItemResourcePatches` | Targeted transpilers for native `BuildingEntity.Update` and `SaveSystem.TryLoadGame` direct resource/instantiate calls. |
| `CustomItemTemplateMarker` | Small marker component identifying runtime templates so the resource/instantiate helpers can activate clones. |
| `IPatchBridge` / `GameAdapterBridge` | Added `TryResolveItemTemplate` so static patches and internal materializers reach the provider. |
| `CuoBootstrap` | Registered `ModContentBinder` as `ICuoService` after mod discovery. |
| `PluginDependencyRegistrar` | Registered the item provider as `IContentBindingProvider`, `ICuoService`, and a `GameAdapter` constructor dependency. |
| Tests | `ModItemDefinitionTests`, `ModContentBinderTests`; extended previous content tests. |
| Protocol version | Unchanged (no wire). |

## 3. Self-check table (mechanism × change × evidence)

| Mechanism | Change | Evidence |
|---|---|---|
| DTO round-trip | `ModItemDefinition.ToPayload`/`FromPayload` preserves fields | `ModItemDefinitionTests.RoundTrip_PreservesCoreFields` |
| Runtime template fields | `TemplateId` and `SpawnComponents` survive the DTO round-trip | `ModItemDefinitionTests.RoundTrip_PreservesCoreFields` |
| Invalid payload | Malformed bytes return null instead of throwing | `ModItemDefinitionTests.InvalidPayload_ReturnsNull` |
| Custom Utils.Create patch contract | Both patch classes resolve against the real `Utils.Create` overloads | `PatchContractTests` (via `PatchInventory.BuildContracts`) |
| Native resource transpilers | BuildingEntity.Update and SaveSystem.TryLoadGame contracts resolve and transpiler parameter mapping is accepted | `PatchContractTests` (via `PatchInventory.BuildContracts`) |
| Binder routes by kind | Item entries reach the item provider | `ModContentBinderTests.BindsSharedContentToMatchingProvider` |
| Local/Host-only filter | HostOnly content is not routed to shared providers | `ModContentBinderTests.SkipsContentFromNonSharedMods` |
| One-shot bind | Binder does not rebind on later updates | `ModContentBinderTests.BindsOnlyOnce` |
| Unknown kind | No provider -> skipped without throwing | `ModContentBinderTests.UnknownKind_IsSkippedWithoutProvider` |
| Provider isolation | A throwing provider does not stop other kinds | `ModContentBinderTests.ProviderException_DoesNotStopOtherEntries` |
| DI integration | Real mod stack routes `test.content` item through binder | `ModContentBinderTests.Binder_RoutesRealModContentThroughDi` |
| Content schema version | Existing version storage/validation remains covered | `ModContentTests.SchemaVersion_IsStoredAndRead`, `InvalidSchemaVersion_IsRefused` |
| No wire/protocol regression | Static content remains local; no NetMsg | `docs/api/mod-api.md` §4f, full suite green |

## 4. Verification design

- L0 simulation over real mod stack for binder/DI integration.
- Pure-managed unit tests for DTO serialization, binder routing, shared-mode
  filtering, one-shot behavior, unknown kind, and provider exception
  isolation.
- GameAdapter provider/template/resolver are intentionally not
  compile-referenced by tests (same boundary as other GameAdapter contract
  tests); their behavior is a thin game-facing adapter on top of the tested
  binder contract. The new Harmony patch classes are covered by the
  patch-contract tests, which resolve every `[HarmonyPatch]` against the real
  game assembly.
- Static evidence: no game/Unity type in Abstractions; no new wire message;
  static content is covered by the existing mod handshake consistency check.

## 5. Verification results (2026-09-01)

| Evidence | Result |
|---|---|
| `dotnet build CasualtiesUnknownOnline.slnx` | 0 warnings / 0 errors |
| `dotnet test CasualtiesUnknownOnline.slnx` | 1991 passed / 0 failed |
| `dotnet format CasualtiesUnknownOnline.slnx` | clean |
| `tools/check-architecture.ps1` | pass |
| `tools/check-event-replay.ps1` | pass |
| `tools/check-entity-event-dispatch.ps1` | pass |
| `tools/check-delivery.ps1` | pass |
