# Mod status vanilla moodle-row local seam — phase 3 self-check

Owner cycle: CUCoreLib migration support / mod-status domain.

Decision: add the local GameAdapter seam that feeds active mod status moodles
into the vanilla `MoodleManager`. Static `ModMoodleDefinition` descriptors were
already bound; this round connects them to runtime status presences and to the
vanilla moodle row without adding a wire message or exposing Unity types through
Abstractions.

## 1. Mechanism inventory

| # | Mechanism | Evidence / decision |
|---|---|---|
| 1 | Presence read seam | `ModStatusStore.GetStatusPresences(player)` returns every stored body/limb status presence for one player, including opaque statuses, without copying payload bytes. |
| 2 | Moodle projection | `ModStatusMoodleProjection` resolves active statuses → `ModStatusDefinition.MoodleId` (or a per-limb `LimbMoodles` match) → `ModMoodleDefinition`, validates the vanilla icon/intensity, and calls `MoodleManager.AddMoodle`. |
| 3 | Main/side row split | Important moodles are added in the `MoodleManager.AddAllMoodles` prefix (before the native side-row switch); non-important moodles are added in the postfix (side row). |
| 4 | Dedup/safety | Duplicate moodles are prevented per refresh; per-limb rows use a limb-aware key so each affected limb can appear once; missing icon/out-of-range intensity are skipped and warned once. |
| 5 | Harmony bridge | `ModStatusMoodlePatches.ModMoodlePatch` forwards to `PatchBridge.Impl.ApplyModMoodles(MoodleManager, importantRow)`. |
| 6 | Animated moodle icons | `ModStatusMoodleProjection` resolves `ModMoodleAnimation` frames to a synthetic icon key; `MoodleAnimationRegistry` stores the mapping and `ModStatusMoodlePatches.MoodleAnimationPatch` drives the vanilla moodle UI `Image` through `CustomImageAnimator`. |
| 7 | Per-limb row routing | `ModStatusDefinition.ShowPerLimbMoodles` opts a limb-scoped status into one row per affected limb; `LimbMoodles` maps a limb name to a distinct moodle id; `ModMoodleDefinition.LimbDisplayNameFormat` / `LimbDescriptionFormat` format limb-aware tooltip text. |
| 8 | No wire change | Moodle row feeding is local presentation only; no NetMsg, no protocol bump, no JObject snapshot. |
| 9 | No Abstractions leak | `ModStatusMoodleProjection` lives in GameAdapter and never exposes `MoodleManager`/`Sprite` to mods. |

## 2. Whole-family audit

| Family member | Change |
|---|---|
| `ModStatusStore` | Added `StatusPresence` nested record and `GetStatusPresences(player)`. |
| `ModStatusDefinition` / `ModMoodleDefinition` | Added static per-limb moodle routing fields (`ShowPerLimbMoodles`, `LimbMoodles`) and per-limb display/description templates (`LimbDisplayNameFormat`, `LimbDescriptionFormat`). |
| `ModStatusMoodleProjection` | New GameAdapter moodle-row applier; resolves animated moodle frames into synthetic icon keys and applies per-limb routing/dedup/tooltip templates. |
| `MoodleAnimationRegistry` | New GameAdapter local mapping from synthetic moodle icon keys to resolved frame animations. |
| `CustomImageAnimator` | New GameAdapter MonoBehaviour that drives a vanilla moodle UI `Image` from resolved frames. |
| `ModStatusMoodlePatches` | New Harmony prefix/postfix on `MoodleManager.AddAllMoodles` plus a `Moodle.Start` patch for animated icons. |
| `IPatchBridge` / `GameAdapterBridge` | Added `ApplyModMoodles(MoodleManager, bool)`. |
| `GameAdapterDomains` / `GameAdapter` | Wired status/moodle content providers and moodle projection. |
| Tests | Store presence test + DTO round-trips + provider validation + reflective moodle projection/patch contract tests including the animation patch. |
| Protocol version | Unchanged (no wire). |

## 3. Self-check table

| Mechanism | Change | Evidence |
|---|---|---|
| Presence read | Opaque body/limb statuses are both returned for the requested player | `ModStatusProjectionStoreTests.StatusPresences_IncludeOpaqueAndLimbValues_ForRequestedPlayer` |
| Per-limb routing DTO | `ModStatusDefinition.ResolveMoodleId` honors `LimbMoodles` when per-limb rows are enabled, ignores them otherwise | `ModStatusDefinitionTests.ResolveMoodleId_*` |
| Per-limb format DTO | `ModMoodleDefinition.FormatLimbDisplayName` / `FormatLimbDescription` use authored templates and fall back to plain text | `ModMoodleDefinitionTests.FormatLimbText_UsesAuthoredTemplates_AndFallsBack` |
| Provider validation | Body-scoped per-limb, bindings without per-limb flag, duplicate limbs, and overlong formats are refused | `StatusMoodleContentProviderTests.StatusProvider_ValidatesPerLimbMoodleRouting`, `MoodleProvider_RejectsOverlongLimbDisplayFormats` |
| Moodle applier contract | `ModStatusMoodleProjection.ApplyModMoodles(MoodleManager, bool)` exists | `ModStatusProjectionContractTests.MoodleProjection_HasApplyMethod` |
| Moodle patch contract | `ModStatusMoodlePatches.ModMoodlePatch` has Prefix and Postfix | `ModStatusProjectionContractTests.MoodlePatches_HavePrefixAndPostfix` |
| Animation patch contract | `ModStatusMoodlePatches.MoodleAnimationPatch` has a `Postfix` | `ModStatusProjectionContractTests.MoodleAnimationPatch_HasPostfix` |
| Direct moodle safety | Missing vanilla icon/out-of-range intensity are skipped, not thrown | code path + contract tests |
| No wire change | No new NetMsg / protocol bump | full protocol tests pass |

## 4. Verification results

| Evidence | Result |
|---|---|
| `dotnet build CasualtiesUnknownOnline.slnx` | 0 warnings / 0 errors |
| `dotnet test CasualtiesUnknownOnline.slnx` | 2118 passed / 0 failed |
| `dotnet format CasualtiesUnknownOnline.slnx` | clean |
| `tools/check-architecture.ps1` | pass |
| `tools/check-event-replay.ps1` | pass |
| `tools/check-entity-event-dispatch.ps1` | pass |
| `tools/check-delivery.ps1` | pass |
