# Pinyin search (stage 2: the console's resource-id completion)

Date: 2026-09-20
Scope: pinyin search on the in-game console's completion for resource arguments — the pluggable
extra match stage behind `IResourceLocationCatalog`, the switch path, and the tests. The crafting
search box was stage 1 (`pinyin-search-selfcheck.md`, decision 199); this stage is decision 200.
Both stages are recorded by the pinyin-search ticket.

## What landed

- **The seam is a stage, not catalog logic** (`src/CasualtiesUnknownOnline.Runtime/Session/Content/`):
  `IResourceLocationMatchStage` answers one question about one entry and one normalized prefix;
  `ResourceLocationCatalog` keeps its four built-in ranks (0 exact canonical id, 1 id prefix, 2 bare
  path prefix, 3 display-name prefix) and the `MaxSuggestions = 20` cap, and ranks every registered
  stage after them at 4 + its registration index. A stage is consulted only for entries the built-in
  ranks did not match — equivalent to "ranked after", and it keeps a switched-off stage off the path
  of everything the catalog already matches.
- **The pinyin stage** (`PinyinResourceLocationMatchStage`) matches the entry's `DisplayName` only
  (never the id string) with the shared `PinyinMatcher.Contains`, reads
  `IOptionsMonitor<PinyinSearchOptions>.CurrentValue.Enabled` live per call, and returns before
  touching the reading table while the switch is off.
- **One switch, two surfaces**: the Runtime stage reads the same options-monitor singleton the Game
  Adapter's `PinyinSearchGate` reads for the crafting patch, so a config edit applies to the next
  keystroke on both.
- **Composition**: the content-vocabulary block moved out of `CuoBootstrap` into
  `ContentVocabularyComposition.AddContentVocabulary` (same registrations, same order, same factory
  ownership) and now registers the stage; `CuoBootstrap` is 596 lines against the 600-line cap.
- **Reading-table report**: both surfaces report through `PinyinTableReport.ReportOnce`, so a missing
  embedded table produces one line per process instead of one per surface.
- **User-visible text**: `Search.PinyinSearch`'s BepInEx `ConfigDescription` and the Online UI hint
  `prefs.pinyin_search_hint` (EN + ZH) now name both surfaces and describe the switch-off state
  correctly — the crafting box returns to the game's own rule, the console keeps its catalog ranking.
- **Behaviour admission**: reusing `PinyinMatcher.Contains` brings its literal half, so with the
  switch on an ASCII display name also matches mid-string ("ooden" → "Wooden Sword") — a suggestion
  class the built-in display-name *prefix* rank cannot produce. Additive and switch-gated; recorded
  here because no earlier document said it.

## Mechanism inventory

| Mechanism | Change | Evidence |
|---|---|---|
| Built-in ranks 0-3 and the 20 cap | Unchanged; extra stages rank after them | `ResourceLocationCatalog`; `Suggest_WithoutMatchStages_KeepsTheBuiltInResultsExactly`, `Suggest_ExtraStageMatches_RankAfterEveryBuiltInRank` (one row per built-in rank) |
| Console projection | Unchanged — `CommandConsoleService.SuggestResourceLocations` still projects `entry.Id.ToString()` | `ArgumentSuggestions_ResourceLocationKind_CompletesToTheCanonicalId` |
| Switch | Live per query; off ⇒ no stage match and no table load | `Suggest_SwitchOff_LeavesTheNativeRankingUntouched`, `Suggest_FollowsTheSwitchWithoutRebuildingTheCatalog` |
| Matcher | Reuses `PinyinMatcher.Contains` against `DisplayName`; no second implementation | `Suggest_ReachesTheCanonicalId_FromPinyinInitialsChineseAndIdPrefixes`, `Suggest_MatchesTheDisplayName_NeverTheIdString` |
| Cap safety | A stage cannot push built-in matches out of the result | `Suggest_ExtraStageThatMatchesEverything_CannotCrowdBuiltInMatchesOutOfTheCap` |
| Stage ordering | Registration order, id order inside one rank | `Suggest_ExtraStages_RankInRegistrationOrder`, `Suggest_ExtraStageMatches_AreOrderedByIdWithinTheStageAndStillCapped` (reversed source order) |
| Reading table | Embedded resource, lazily loaded, one process-wide report | `PinyinDictionaryTests`; `PinyinTableReport` |
| Protocol / wire | **Untouched** — protocol stays 34 (a fact, not a design input) | no message type changed |

## Verification design

- **Focused**: `dotnet test CasualtiesUnknownOnline.slnx --filter "FullyQualifiedName~ResourceLocationCatalog|FullyQualifiedName~Pinyin|FullyQualifiedName~NameSearchMatcher|FullyQualifiedName~RecipeSearchDecision|FullyQualifiedName~CommandConsoleCompletion"` → **104 passed / 0 failed**.
- **Normative gates**: **69 passed / 0 failed**.
- **Full suite with build**: `CasualtiesUnknownOnline.Tests` **3617 passed / 0 failed** (previous cycle 3588 + this change's 29 cases) plus the gates project's 69 in the same run; `dotnet format` exits 0.
- **Independent adversarial review** (fresh context, frozen tree, report on disk at
  `%TEMP%\cuo-review-pinyin-stage2.md`): 0 blocker / 4 major / 6 minor / 4 nit. Every finding is
  fixed here — the majors were stale shipped texts (the plugin's own comment on the option, its
  player-visible `ConfigDescription`, and two evidence documents that still called the console half
  unbuilt), not mechanism defects. The review also re-derived every number in this file from the
  frozen tree.
- **Deployment**: `tools/deploy.ps1` then
  `tools/verify-deploy.ps1 -GameDir "<game-dir>"` → exit 0, delivered artifact
  `0.1.0+ee0a5fe7674a07113f96522d9ad33c01246c82ac` == this change's commit, so what runs on the
  machine is this tree's build output.

## Test coverage split (stated exactly)

- **Covered by tests**: the ranking contract (a stage ranks after each of the four built-in ranks, in
  registration order, id-ordered inside a rank, capped, and never displacing built-in matches), the
  pinyin stage itself (the acceptance queries, switch off, the live switch, display-name-only
  matching, the inherited ASCII literal path, empty name/prefix), and the production composition
  (exactly one stage registered; `ftn` / `fent` / `芬太` / `cu:fent` each complete to `cu:fentanyl`).
- **Not covered by any test**: the real entry source. `VanillaItemResourceLocationSource` lives in
  the Game Adapter, which the test project excludes from compilation, so the tests feed a stub entry
  with a Chinese display name. That the game's localized item name is Chinese on a Chinese client is
  stage 1's decompiled evidence (`Item.cs` assigns `Locale.GetItem(id)`), not re-proven here.

## What the user must verify (not provable by automation)

- The console's completion popup while typing `ftn` / `fent` / `芬太` / `cu:fent`, the accepted token
  being the canonical id, and the row ordering/count on a Chinese client.
- That the switch reaches the console without a restart, and that the crafting box still behaves as
  stage 1 verified.
- The updated BepInEx config description and the Preferences hint text rendering as intended.

## Limits (recorded, not hidden)

- The ASCII mid-string widening is additive and gated, but it is a suggestion class the console could
  not produce before; it is admitted here and in the stage's code doc, and it is exercised at catalog
  level only — no test drives it through the console projection.
- **"Off ⇒ no reading-table load" is a code fact, not a test.** `PinyinDictionary` is a
  process-global lazy static, so a suite that already ran any pinyin test cannot observe a
  first-load refusal. The tests pin the result half; the guard order (the enabled check returns
  before `PinyinTableReport`/the matcher) is read from the code.
- The Debug hit log sits below the default `Information` level, so the runtime trace needs an
  explicit level change; no test observes it.
- The reading table is third-party data: a character outside it silently degrades to the literal
  substring rule instead of failing.
- Stage 1's crafting-surface gaps (the read-path signal's scope, the item-filter field probe) are
  unchanged by this stage.
