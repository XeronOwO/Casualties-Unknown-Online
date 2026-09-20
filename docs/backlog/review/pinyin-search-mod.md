# CasualtiesUnknownOnline.Pinyin: pinyin search for CUO

- Status: Review
- Priority: Medium
- Category: Mod / Tooling / UI / Pinyin / Console

Port the pinyin search behavior from the standalone `JustUnknownCharacters`
project into CUO. Working name: `CasualtiesUnknownOnline.Pinyin`; a more
conventional CUO-ecosystem name can be decided during implementation.

Source project (the reference implementation this feature was ported from):

- `JustUnknownCharacters` provides:
  - `PinyinMatcher` — PinIn NFA/backtracking matcher for full pinyin, initials,
    mixed Chinese/pinyin, and fuzzy tone handling.
  - `PinyinDict` — embedded pinyin syllable dictionary.
  - Harmony patch over the native crafting recipe search (`PlayerCamera.RefreshRecipeList`).

Planned features:

1. **Crafting UI pinyin search**
   - Port the native recipe-list filtering behavior from `JustUnknownCharacters`.
   - The crafting search box should match recipe names by full pinyin,
     initials, single-character pinyin, and mixed Chinese/pinyin input.
   - Examples from the source project:
     - `xianweisheng` / `xws` / `sheng` / `纤wei` / `xian维` → 纤维绳.

2. **Command completion pinyin search**
   - Build on the command completion work in
     [Command completion for the ID system](command-id-name-completion.md).
   - Support matching by **id / name / pinyin**.
   - The matching chain is: pinyin input (`fent`/`ftn`) → Chinese display
     name (`芬太尼`) → canonical id (`cu:fentanyl`).
   - Pinyin is matched against the Chinese/localized name, not against the id
     string itself.
   - This part requires **extra interface extraction** before implementation:
     the pinyin matcher/catalog should be pluggable behind a clean seam instead
     of being hard-coded into `CommandConsoleService` or the content catalog.
     The namespaced-id cycle landed the seam it builds on:
     `IResourceLocationCatalog` / `IResourceLocationSource`
     (`src/CasualtiesUnknownOnline.Runtime/Session/Content/`) already own the
     canonical id + display-name matching, so pinyin is a new matcher/rank stage
     rather than a rewrite. The exact interface shape should be decided during
     implementation; candidates include a resource-id search provider and a
     completion-source extension point in Abstractions/Runtime.

3. **Pinyin search on/off switch**
   - Pinyin search must be a configurable toggle.
   - The default value is derived from the player's system language:
     - Simplified Chinese system language → enabled by default.
     - Other system languages → disabled by default.
   - The player should still be able to override the value through the config.

## Progress

### Stage 1 — crafting search + the switch (landed)

- **Matcher core is CUO's own, in Runtime** (`Runtime/Search/`):
  `PinyinDictionary` (+ `PinyinSyllable`) loads the vendored reading table from
  an embedded resource and expands the fuzzy alternates at load;
  `PinyinMatcher` is the NFA/backtracking port; `NameSearchMatcher` is the
  predicate every surface shares (native ordinal-ignore-case substring OR
  pinyin). Nothing in Runtime knows about the game.
- **The crafting half reuses the game's own search box.** The native filter is
  `simpleName.Contains(recipeFilter, OrdinalIgnoreCase)` inside `RefreshRecipeList`,
  so the extension lands on the name that predicate reads: a one-refresh query
  scope is opened by the patch's prefix and closed by its postfix **and** a
  finalizer, and inside it `Recipe.get_simpleName` answers "" for a recipe that
  does not match. Ordering, the category filter, the row objects, the scroll
  position and the selection index stay native — no row surgery, no duplicated
  layout math. The scope is skipped when the camera's item filter is active
  (the native refresh ignores the text filter then).
- **Switch**: `Search.PinyinSearch`, a local BepInEx option whose default
  follows the system language (Simplified Chinese → on), read through the Game
  Adapter's construction-time gate (the monitor refreshes it on the config's
  change event); the Online UI Preferences page writes it. Off means the
  crafting search keeps the native behavior untouched.
- **Test coverage split, stated exactly**: the matcher core and the pure
  decision (`RecipeSearchDecision` — switch, typed query, item-filter
  precedence, including the switch-off path) are covered by
  `PinyinDictionaryTests` / `PinyinMatcherTests` / `NameSearchMatcherTests` /
  `RecipeSearchDecisionTests`. The game-coupled half — the two patches, the
  scope, the static gate and the BepInEx wiring — has NO automated test: the
  test project excludes the Game Adapter from compilation, so its evidence is
  the patch-contract check plus in-game acceptance.
- Reading table provenance: PinIn (the table behind the standalone mod),
  vendored whole as `Runtime/Search/pinyin_data.txt`.
- **Failure signal, with its exact scope**: the patch contract can only prove
  the hook is installed, so the scope counts the names each refresh consults
  and warns once after three consecutive refreshes with an active query that
  consulted none — either the recipe list was empty or the native filter no
  longer reads `Recipe.simpleName`. **Not covered**: a predicate that switched
  to a different name while the row-building loop keeps reading `simpleName`
  keeps the counter fed — a recorded gap, not a guarded one.
- **Structure/naming outcome**: there is no separate
  `CasualtiesUnknownOnline.Pinyin` assembly — the matcher core lives in
  `Runtime/Search/`, the game patch in `GameAdapter/Patches/`, and the switch is
  a CUO option. A standalone plugin could not patch this surface at all: only
  the Game Adapter may reference the game assemblies (`docs/api/mod-api.md` §1).
- Evidence: `PinyinDictionaryTests`, `PinyinMatcherTests`,
  `NameSearchMatcherTests`, `RecipeSearchDecisionTests` and
  `docs/evidence/selfchecks/search/pinyin-search-selfcheck.md`.
- Deployment: `tools/deploy.ps1` then
  `tools/verify-deploy.ps1 -GameDir "<game-dir>"` → exit 0, `delivered artifact:
  0.1.0+74a171e3232691883241c4bf4879cbb2c9e89166` (this change's commit), so the
  running plug-in is this tree's build output.

### Stage 2 — console completion (landed)

- The console's resource completion adds pinyin as an **extra ranking stage**
  behind the existing `IResourceLocationCatalog` seam
  (`IResourceLocationMatchStage` + `PinyinResourceLocationMatchStage`), ranked
  after the canonical-id / bare-path / display-name stages, so `/cmd cu:fent`,
  `fent` and `ftn` all reach `cu:fentanyl` while the accepted suggestion stays
  the canonical id. The console's own projection
  (`CommandConsoleService.SuggestResourceLocations`) is unchanged, so it emits
  canonical ids only.
- The stage matches the entry's display name (never the id string) with the
  shared `PinyinMatcher.Contains`, reads `Search.PinyinSearch` live through
  `IOptionsMonitor<PinyinSearchOptions>` — the same singleton the crafting
  patch's gate reads — and while the switch is off it matches nothing and loads
  no reading table. The pinyin stage is consulted only for entries the four
  built-in ranks did not match, so it can never displace or reorder them.
- Reusing the shared matcher also brings its literal half: with the switch on an
  ASCII display name matches mid-string ("ooden" → "Wooden Sword"), which the
  built-in display-name *prefix* rank never did. Additive and switch-gated —
  recorded here because no earlier document admitted it.
- Both surfaces now report the reading table through
  `PinyinTableReport.ReportOnce`, so a missing embedded table produces one line
  per process instead of one per surface.
- The content-vocabulary registrations moved from `CuoBootstrap` into
  `ContentVocabularyComposition.AddContentVocabulary` (same registrations, same
  order, same factory ownership) to stay inside the composition root's 600-line
  architecture cap; `CuoBootstrap` is 596 lines.
- Evidence: `ResourceLocationCatalogTests` (the ranking contract),
  `PinyinResourceLocationMatchStageTests` (the stage), `CommandConsoleCompletionTests`
  (the production composition) and
  `docs/evidence/selfchecks/search/pinyin-console-completion-selfcheck.md`;
  decision 200 records the mechanism.
- Deployment: `tools/deploy.ps1` then
  `tools/verify-deploy.ps1 -GameDir "<game-dir>"` → exit 0, `delivered artifact:
  0.1.0+ee0a5fe7674a07113f96522d9ad33c01246c82ac` (this change's commit), so the
  running plug-in is this tree's build output.

## Limits (recorded, not hidden)

- The in-game rendering and the frame-level feel of the native search box
  (rows appearing/disappearing per keystroke, tooltips, scroll position) can
  only be verified by the user in the real game. What the automated tests cover
  is the matcher core and the pure decision — NOT the patch, the scope, the
  static gate or the BepInEx wiring (the test project excludes the Game Adapter
  from compilation).
- The English and Chinese localization tables have no key-set parity gate: the
  four new keys were verified by inspection, and a future missing key falls back
  to English silently.
- The reading table is a vendored third-party dataset: it covers 26k+
  characters, but a character outside it silently degrades to the literal
  substring rule rather than failing.
- The console half's entry source has no automated coverage:
  `VanillaItemResourceLocationSource` lives in the Game Adapter, which the test
  project excludes from compilation, so the tests feed a stub entry carrying a
  Chinese display name. That a Chinese client's localized item name really is
  Chinese remains stage 1's decompiled evidence, not re-proven here.
- The console's pinyin matching widens the ASCII path as well: with the switch on
  a display name matches mid-string ("ooden" → "Wooden Sword"), which the
  built-in display-name rank never did. Additive and gated, admitted here rather
  than discovered later.
