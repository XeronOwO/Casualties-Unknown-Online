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
| 2 | Moodle projection | `ModStatusMoodleProjection` resolves active statuses → `ModStatusDefinition.MoodleId` → `ModMoodleDefinition`, validates the vanilla icon/intensity, and calls `MoodleManager.AddMoodle`. |
| 3 | Main/side row split | Important moodles are added in the `MoodleManager.AddAllMoodles` prefix (before the native side-row switch); non-important moodles are added in the postfix (side row). |
| 4 | Dedup/safety | Duplicate moodles are prevented per refresh; missing icon/out-of-range intensity are skipped and warned once. |
| 5 | Harmony bridge | `ModStatusMoodlePatches.ModMoodlePatch` forwards to `PatchBridge.Impl.ApplyModMoodles(MoodleManager, importantRow)`. |
| 6 | No wire change | Moodle row feeding is local presentation only; no NetMsg, no protocol bump, no JObject snapshot. |
| 7 | No Abstractions leak | `ModStatusMoodleProjection` lives in GameAdapter and never exposes `MoodleManager`/`Sprite` to mods. |

## 2. Whole-family audit

| Family member | Change |
|---|---|
| `ModStatusStore` | Added `StatusPresence` nested record and `GetStatusPresences(player)`. |
| `ModStatusMoodleProjection` | New GameAdapter moodle-row applier. |
| `ModStatusMoodlePatches` | New Harmony prefix/postfix on `MoodleManager.AddAllMoodles`. |
| `IPatchBridge` / `GameAdapterBridge` | Added `ApplyModMoodles(MoodleManager, bool)`. |
| `GameAdapterDomains` / `GameAdapter` | Wired status/moodle content providers and moodle projection. |
| Tests | Store presence test + reflective moodle projection/patch contract tests. |
| Protocol version | Unchanged (no wire). |

## 3. Self-check table

| Mechanism | Change | Evidence |
|---|---|---|
| Presence read | Opaque body/limb statuses are both returned for the requested player | `ModStatusProjectionStoreTests.StatusPresences_IncludeOpaqueAndLimbValues_ForRequestedPlayer` |
| Moodle applier contract | `ModStatusMoodleProjection.ApplyModMoodles(MoodleManager, bool)` exists | `ModStatusProjectionContractTests.MoodleProjection_HasApplyMethod` |
| Moodle patch contract | `ModStatusMoodlePatches.ModMoodlePatch` has Prefix and Postfix | `ModStatusProjectionContractTests.MoodlePatches_HavePrefixAndPostfix` |
| Direct moodle safety | Missing vanilla icon/out-of-range intensity are skipped, not thrown | code path + contract tests |
| No wire change | No new NetMsg / protocol bump | full protocol tests pass |

## 4. Verification results

| Evidence | Result |
|---|---|
| `dotnet build CasualtiesUnknownOnline.slnx` | 0 warnings / 0 errors |
| `dotnet test CasualtiesUnknownOnline.slnx` | 2072 passed / 0 failed |
| `dotnet format CasualtiesUnknownOnline.slnx` | clean |
| `tools/check-architecture.ps1` | pass |
| `tools/check-event-replay.ps1` | pass |
| `tools/check-entity-event-dispatch.ps1` | pass |
| `tools/check-delivery.ps1` | pass |
