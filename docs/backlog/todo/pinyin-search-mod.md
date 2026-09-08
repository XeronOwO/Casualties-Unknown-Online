# CasualtiesUnknownOnline.Pinyin: pinyin search for CUO

- Status: Todo
- Priority: Medium
- Category: Mod / Tooling / UI / Pinyin / Console

Port the pinyin search behavior from the standalone `JustUnknownCharacters`
project into a CUO mod. Working name: `CasualtiesUnknownOnline.Pinyin`; a more
conventional CUO-ecosystem name can be decided during implementation.

Source project (record only, no code yet):

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

Not started; record only.
