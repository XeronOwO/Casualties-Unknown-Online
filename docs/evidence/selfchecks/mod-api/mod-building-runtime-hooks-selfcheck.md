# Mod building runtime hooks — mechanism inventory and self-check

Owner cycle: CUCoreLib migration support — custom building prefab/instance
configuration after typed building content, drops, and worldgen density
landed.

Decision: add an abstraction-safe runtime building hook seam as the CUO
replacement for CUCoreLib's `ConfigurePrefab` / `ConfigureInstance`
GameObject callbacks. A mod registers per-building hooks through
`IModBuildingRuntime`; each hook receives a plain request and returns
component type names. The Game Adapter attaches those components at the
inactive runtime template (prefab hook) or on a newly instantiated building
before it becomes active (instance hook). No game/Unity type or live
GameObject crosses Abstractions and no wire message is added.

## 1. Mechanism inventory

| # | Mechanism | Evidence / decision |
|---|---|---|
| 1 | Plain hook requests | `ModBuildingPrefabRequest` carries building id and template id; `ModBuildingInstanceRequest` additionally carries world X/Y/Z rotation. Both are plain Abstractions DTOs with no game or Unity type. |
| 2 | Mod-facing runtime surface | `IModBuildingRuntime` exposes prefab and instance hook registration/unregistration/query/counts, scoped implicitly to the calling mod. |
| 3 | Runtime hook table | `ModBuildingRuntimeStore` keeps per-mod `building id -> delegate` tables, validates ids, rejects duplicates, and enforces a per-mod hook cap. |
| 4 | Per-mod adapter | `ModBuildingRuntimeAdapter` wraps the store with the mod manifest id and framework logging, following the existing `IModMoodleRuntime` pattern. |
| 5 | Owner scoping | `GameAdapterBuildingContentProvider` records the owning mod id when it binds a building definition and only consults that owner's hook table, so non-owner registrations are stored but never applied. |
| 6 | Prefab hook application | The provider invokes the owner's prefab hook after `CustomBuildingTemplateFactory` builds the inactive template and before the template is cached. |
| 7 | Instance hook application | `UtilsCreateCustomPrefabPatch` calls `IPatchBridge.ApplyCustomBuildingInstanceHooks` on the instantiated clone before `SetActive(true)`; the bridge forwards to the provider. |
| 8 | Component-only result | Hooks return `IReadOnlyList<string>?` component type names; `CustomComponentAttach` already resolves/validates/attaches them and skips duplicates. |
| 9 | Failure isolation | A throwing hook is logged once per building id/phase and disabled for that id; the template/instance continues without the hook contribution. |
| 10 | No wire | Hooks are local-only, process-local, and add no new NetMsg, protocol bump, or generic snapshot. |

## 2. Whole-family audit

| Family member | Change |
|---|---|
| `IModBuildingRuntime` | New Abstractions interface for per-mod prefab/instance hooks. |
| `ModBuildingPrefabRequest` | New Abstractions request DTO for prefab hooks. |
| `ModBuildingInstanceRequest` | New Abstractions request DTO for instance hooks. |
| `IModContext.BuildingRuntime` | New mod-context surface exposing the runtime building hooks. |
| `ModBuildingRuntimeStore` | New Runtime per-mod hook table (shared with GameAdapter). |
| `ModBuildingRuntimeAdapter` | New per-mod adapter for the building hook surface. |
| `ModService` / `ModLifecycle` / `ModContext` | Wired the store and adapter into the mod lifecycle. |
| `CuoBootstrap` | Registered the shared building runtime hook store. |
| `GameAdapterBuildingContentProvider` | Tracks building owners, applies prefab hooks, and exposes instance hook application. |
| `IPatchBridge` / `GameAdapterBridge` | Added `ApplyCustomBuildingInstanceHooks` forwarding seam. |
| `UtilsCreateCustomPrefabPatch` | Applies instance hooks before activating custom building clones. |
| Tests | `ModBuildingRuntimeTests`, updated `BuildingWorldGenProviderTests` constructor. |
| Protocol version | Unchanged (no wire). |

## 3. Self-check table

| Mechanism | Change | Evidence |
|---|---|---|
| Happy path | Prefab and instance hooks register, enumerate, query, unregister | `ModBuildingRuntimeTests.Register_Has_Unregister_HappyPath` |
| Validation | Null/invalid/duplicate registrations are refused for both hook kinds | `ModBuildingRuntimeTests.Register_RejectsNullInvalidAndDuplicate` |
| No binding requirement | Hooks may be registered before building content is bound | `ModBuildingRuntimeTests.Hooks_DoNotRequireBuildingContentBinding` |
| Mod scoping | Store returns only the calling mod's delegates; other mod ids miss | `ModBuildingRuntimeTests.Store_ReturnsRegisteredDelegatesAndScopesByModImplicitly` |
| Provider construction | The building provider can be constructed with the shared runtime store | `BuildingWorldGenProviderTests` continues to validate worldgen/drop contracts |
| No wire/protocol regression | Existing content/spawn/local surfaces unchanged; no NetMsg added | `docs/api/mod-api.md`, full suite green |

## 4. Verification design

- Pure-managed tests cover the runtime hook table/adapter and the provider's
  reflectively observable worldgen/drop contracts; no Unity world is required.
- The actual `Utils.Create` materialization path remains behind the GameAdapter
  boundary and is covered by the existing patch-contract tests and the bridge
  forwarding seam; no new wire path is introduced.
- The hook result is intentionally component names only. A mod-authored
  component still owns its own initialization; arbitrary direct GameObject
  mutation remains a non-goal for Abstractions.

## 5. Verification results

| Evidence | Result |
|---|---|
| `dotnet build CasualtiesUnknownOnline.slnx` | 0 warnings / 0 errors |
| `dotnet test CasualtiesUnknownOnline.slnx` | 2126 passed / 0 failed |
| `dotnet format CasualtiesUnknownOnline.slnx` | clean |
| `tools/check-architecture.ps1` | pass |
| `tools/check-event-replay.ps1` | pass |
| `tools/check-entity-event-dispatch.ps1` | pass |
| `tools/check-delivery.ps1` | pass |
