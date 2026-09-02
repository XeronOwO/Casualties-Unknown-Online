# Mod status/moodle static content binding — mechanism inventory and self-check

Owner cycle: CUCoreLib migration support — typed static descriptors for status
and moodle after the item/recipe/liquid/building/tile/structure content family.

Decision: add the seventh and eighth typed Abstractions DTOs
(`ModStatusDefinition` / `ModMoodleDefinition`) plus GameAdapter content
providers that validate and store the static descriptors. This is the migration
base for CUCoreLib-style statuses and moodles. It deliberately does **not**
create per-player/per-limb runtime status values or feed the vanilla moodle
manager: those are dynamic/UI concerns that first need the host-authoritative
mod-data domain boundary and therefore remain separate future work.

## 1. Mechanism inventory

| # | Mechanism | Evidence / decision |
|---|---|---|
| 1 | Status DTO | `ModStatusDefinition` in `CUO.Abstractions` with body/limb scope, save metadata, optional moodle id, and `CustomData`. |
| 2 | Scope enum | `ModStatusScope.Body` / `ModStatusScope.Limb` keeps the CUCoreLib body-vs-limb distinction without coupling Abstractions to game types. |
| 3 | Moodle DTO | `ModMoodleDefinition` with intensity, stable icon key, display text, critical/chipped/important flags, hold seconds, and `CustomData`. No Unity `Sprite` in Abstractions. |
| 4 | Status provider | `GameAdapterStatusContentProvider` decodes, validates, and stores the static descriptor registry. |
| 5 | Moodle provider | `GameAdapterMoodleContentProvider` decodes, validates, and stores the static descriptor registry. |
| 6 | Shared-content filter | Existing `ModContentBinder` applies the same shared-content network-mode filter as all other static content kinds. |
| 7 | Runtime boundary kept open | The provided registries do not attach to `PlayerState`, `Body`, or `Limb`; dynamic per-player values are explicitly deferred to the mod-data sync model. |
| 8 | No wire | Status/moodle descriptors are static content; no new NetMsg or snapshot protocol. |

## 2. Whole-family audit

| Family member | Change |
|---|---|
| `ModStatusScope` | New Abstractions enum. |
| `ModStatusDefinition` | New Abstractions DTO + serialization helpers. |
| `ModMoodleDefinition` | New Abstractions DTO + serialization helpers. |
| `GameAdapterStatusContentProvider` | New GameAdapter provider (validation + static registry). |
| `GameAdapterMoodleContentProvider` | New GameAdapter provider (validation + static registry). |
| `PluginDependencyRegistrar` | Registered both providers as `IContentBindingProvider` and `ICuoService`. |
| `docs/api/mod-api.md` | Added typed status/moodle content + current binding scope. |
| Tests | `ModStatusDefinitionTests`, `ModMoodleDefinitionTests`, reflective `StatusMoodleContentProviderTests`. |
| Protocol version | Unchanged (no wire). |

## 3. Self-check table

| Mechanism | Change | Evidence |
|---|---|---|
| Status DTO round-trip | `ModStatusDefinition.ToPayload`/`FromPayload` preserves scope/save/moodle/custom data | `ModStatusDefinitionTests.RoundTrip_*` |
| Moodle DTO round-trip | `ModMoodleDefinition.ToPayload`/`FromPayload` preserves presentation fields | `ModMoodleDefinitionTests.RoundTrip_*` |
| Invalid payloads | Malformed bytes return null | `ModStatusDefinitionTests.InvalidPayload_ReturnsNull`, `ModMoodleDefinitionTests.InvalidPayload_ReturnsNull` |
| Status provider validation | Body/limb descriptors bind; malformed payload refused | `StatusMoodleContentProviderTests.StatusProvider_*` |
| Moodle provider validation | Valid descriptor binds; missing icon/negative numeric fields refused | `StatusMoodleContentProviderTests.MoodleProvider_*` |
| Binder routing | New providers join the generic kind-routing provider map | full build + existing `ModContentBinderTests` |
| No Abstractions leak | DTOs contain no Unity/game type; providers remain in GameAdapter | `docs/api/mod-api.md`, full build |

## 4. Verification design

- Pure-managed DTO round-trip and invalid-payload tests.
- Reflective GameAdapter provider contract tests (no Unity world needed).
- Static evidence: no new wire message, no per-player status state, no
  Unity `Sprite` in Abstractions, no CUCoreLib source committed.

## 5. Verification results

| Evidence | Result |
|---|---|
| `dotnet build CasualtiesUnknownOnline.slnx` | 0 warnings / 0 errors |
| `dotnet test CasualtiesUnknownOnline.slnx` | 2032 passed / 0 failed |
| `dotnet format CasualtiesUnknownOnline.slnx` | clean |
| `tools/check-architecture.ps1` | pass |
| `tools/check-event-replay.ps1` | pass |
| `tools/check-entity-event-dispatch.ps1` | pass |
| `tools/check-delivery.ps1` | pass |
