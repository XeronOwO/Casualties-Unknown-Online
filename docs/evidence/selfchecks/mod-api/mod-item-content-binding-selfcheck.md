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
| 4 | Game Adapter item provider | `GameAdapterItemContentProvider` accepts `ModItemDefinition`, waits for `Item.GlobalItems`, and injects a static `ItemInfo` into the vanilla item table. |
| 5 | Runtime/permission path | Existing `IModContent` permission/version/opaque-byte rules still apply; content bytes never cross the wire. |
| 6 | DI wiring | `CuoBootstrap` registers the binder as an `ICuoService` after `ModService`; the plugin registers the Game Adapter provider. |

## 2. Whole-family audit

| Family member | Change |
|---|---|
| `ModItemDefinition` | New Abstractions DTO + serialization helpers. |
| `IContentBindingProvider` | New Runtime → Game Adapter boundary for one content kind. |
| `ModContentBinder` | New generic one-shot binder with kind/provider routing and shared-mode filtering. |
| `GameAdapterItemContentProvider` | New GameAdapter provider registering vanilla `ItemInfo`. |
| `CuoBootstrap` | Registered `ModContentBinder` as `ICuoService` after mod discovery. |
| `PluginDependencyRegistrar` | Registered the item provider as `IContentBindingProvider` and `ICuoService`. |
| Tests | `ModItemDefinitionTests`, `ModContentBinderTests`; extended previous content tests. |
| Protocol version | Unchanged (no wire). |

## 3. Self-check table (mechanism × change × evidence)

| Mechanism | Change | Evidence |
|---|---|---|
| DTO round-trip | `ModItemDefinition.ToPayload`/`FromPayload` preserves fields | `ModItemDefinitionTests.RoundTrip_PreservesCoreFields` |
| Invalid payload | Malformed bytes return null instead of throwing | `ModItemDefinitionTests.InvalidPayload_ReturnsNull` |
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
- GameAdapter provider is intentionally not compile-referenced by tests
  (same boundary as other GameAdapter contract tests); its behavior is a thin
  game-facing adapter on top of the tested binder contract.
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
