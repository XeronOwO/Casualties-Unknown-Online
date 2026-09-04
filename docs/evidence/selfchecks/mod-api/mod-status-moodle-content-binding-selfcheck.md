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
| 1 | Status DTO | `ModStatusDefinition` in `CUO.Abstractions` with body/limb scope, save metadata, optional moodle id, optional per-limb moodle routing (`ShowPerLimbMoodles`/`LimbMoodles`), and `CustomData`. |
| 2 | Scope enum | `ModStatusScope.Body` / `ModStatusScope.Limb` keeps the CUCoreLib body-vs-limb distinction without coupling Abstractions to game types. |
| 3 | Moodle DTO | `ModMoodleDefinition` with intensity, stable icon key, display text, critical/chipped/important flags, hold seconds, optional `ModMoodleAnimation` frame-path animation, optional limb display/description templates (`LimbDisplayNameFormat` / `LimbDescriptionFormat`), and `CustomData`. No Unity `Sprite` in Abstractions. |
| 4 | Status provider | `GameAdapterStatusContentProvider` decodes, validates, and stores the static descriptor registry. |
| 5 | Moodle provider | `GameAdapterMoodleContentProvider` decodes, validates (including icon-animation fps/frames), and stores the static descriptor registry. |
| 6 | Shared-content filter | Existing `ModContentBinder` applies the same shared-content network-mode filter as all other static content kinds. |
| 7 | Runtime boundary kept open | The provided registries do not attach to `PlayerState`, `Body`, or `Limb`; dynamic per-player values are explicitly deferred to the mod-data sync model. |
| 8 | No wire | Status/moodle descriptors are static content; no new NetMsg or snapshot protocol. |

## 2. Whole-family audit

| Family member | Change |
|---|---|
| `ModStatusScope` | New Abstractions enum. |
| `ModStatusDefinition` | New Abstractions DTO + serialization helpers; now includes per-limb moodle routing fields and `ResolveMoodleId`. |
| `ModMoodleDefinition` | New Abstractions DTO + serialization helpers; now includes limb display/description templates and formatting helpers. |
| `ModLimbMoodleBinding` | New Abstractions DTO mapping a vanilla limb name to a moodle id. |
| `ModMoodleAnimation` | New Abstractions DTO for ordered moodle icon animation frames. |
| `GameAdapterStatusContentProvider` | New GameAdapter provider (validation + static registry). |
| `GameAdapterMoodleContentProvider` | New GameAdapter provider (validation + static registry). |
| `PluginDependencyRegistrar` | Registered both providers as `IContentBindingProvider` and `ICuoService`. |
| `docs/api/mod-api.md` | Added typed status/moodle content + current binding scope. |
| Tests | `ModStatusDefinitionTests`, `ModMoodleDefinitionTests`, reflective `StatusMoodleContentProviderTests`. |
| Protocol version | Unchanged (no wire). |

## 3. Self-check table

| Mechanism | Change | Evidence |
|---|---|---|
| Status DTO round-trip | `ModStatusDefinition.ToPayload`/`FromPayload` preserves scope/save/moodle/per-limb routing/custom data | `ModStatusDefinitionTests.RoundTrip_*` |
| Status per-limb resolve | Resolve honors `LimbMoodles` only when per-limb rows are enabled | `ModStatusDefinitionTests.ResolveMoodleId_*` |
| Moodle DTO round-trip | `ModMoodleDefinition.ToPayload`/`FromPayload` preserves presentation fields including `IconAnimation` and limb text templates | `ModMoodleDefinitionTests.RoundTrip_*` |
| Moodle format helpers | Limb title/description templates replace `{name}`/`{description}`/`{limb}` and fall back to plain text | `ModMoodleDefinitionTests.FormatLimbText_UsesAuthoredTemplates_AndFallsBack` |
| Invalid payloads | Malformed bytes return null | `ModStatusDefinitionTests.InvalidPayload_ReturnsNull`, `ModMoodleDefinitionTests.InvalidPayload_ReturnsNull` |
| Status provider validation | Body/limb descriptors bind; malformed payload refused | `StatusMoodleContentProviderTests.StatusProvider_*` |
| Moodle provider validation | Valid descriptor with/without icon animation binds; missing icon, negative numeric fields, invalid animation fps/frames refused | `StatusMoodleContentProviderTests.MoodleProvider_*` |
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
| `dotnet test CasualtiesUnknownOnline.slnx` | 2118 passed / 0 failed |
| `dotnet format CasualtiesUnknownOnline.slnx` | clean |
| `tools/check-architecture.ps1` | pass |
| `tools/check-event-replay.ps1` | pass |
| `tools/check-entity-event-dispatch.ps1` | pass |
| `tools/check-delivery.ps1` | pass |
