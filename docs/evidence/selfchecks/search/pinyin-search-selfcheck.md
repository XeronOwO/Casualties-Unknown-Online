# Pinyin search (stage 1: the crafting search box and the switch)

Date: 2026-09-20
Scope: pinyin search on the game's own crafting search box — the matcher core, the pure scope
decision, the native-surface patch, and the opt-in switch whose default follows the system language
(decision 199, `docs/backlog/todo/pinyin-search-mod.md` stage 1). The console's resource completion is
stage 2 and is NOT implemented by this cycle.

## What landed

- **Runtime matcher core** (`src/CasualtiesUnknownOnline.Runtime/Search/`): `PinyinDictionary` loads
  the vendored reading table from the embedded `pinyin_data.txt` (26,585 lines, 328,622 bytes) and
  expands the fuzzy alternates at load; `PinyinSyllable` is one reading; `PinyinMatcher` is the
  PinIn NFA/backtracking port (`Contains`); `NameSearchMatcher` is the predicate every surface
  shares — the native ordinal-ignore-case substring rule OR pinyin. No game type is referenced.
- **`RecipeSearchDecision`** (same folder): the pure decision the patch layer asks — switch on, a
  query typed, and no item filter replacing the text filter. It exists so the whole matrix,
  including the switch-off path, is testable without the game.
- **The native crafting search box is reused, not replaced.** The native filter is
  `r.Item1.simpleName.Contains(this.recipeFilter, 5)` inside `PlayerCamera.RefreshRecipeList`, and
  `Recipe.simpleName` is the game-localised name (`Locale.GetItem(result.id)`), so the extension
  lands on the operand the native predicate already reads: a one-refresh query scope is opened by
  the patch's prefix and closed by its postfix **and** a Harmony finalizer, and inside that scope
  `Recipe.get_simpleName` answers `""` for a recipe whose name does not match the query.
- **Game Adapter**: `PinyinSearchPatches` (two `[HarmonyPatch]` classes, one target each:
  `PlayerCamera.RefreshRecipeList`, `Recipe.get_simpleName`), `RecipeSearchScope` (the borrowed
  query, the `recipeItemFilter` guard, the read-path signal), `PinyinSearchGate` (the switch, bound
  at construction like `PatchBridge`). `GameAdapter` gained one constructor parameter and binds and
  unbinds the gate.
- **Switch**: `Search.PinyinSearch` (`PluginDependencyRegistrar`), default from
  `Application.systemLanguage == ChineseSimplified`; read through
  `IOptionsMonitor<PinyinSearchOptions>` (refreshed by the config's change event, not by polling);
  written by a toggle row on the Online UI **Preferences** page (`OnlineUiPreferencesDrawer` +
  `PinyinSearchConfigEditor`) and persisted immediately. Localization: 4 new keys in both tables
  (`prefs.pinyin_search`, `prefs.pinyin_search_current`, `prefs.pinyin_search_hint`, `common.on`).
- **Reading table provenance**: vendored from PinIn (the table behind the standalone
  `JustUnknownCharacters` mod), embedded as an `EmbeddedResource`; loaded lazily, so a disabled
  switch never pays for it.

## Mechanism inventory

| Mechanism | Change | Evidence |
|---|---|---|
| Native recipe filter predicate | **Re-used**: the filter itself is untouched; the name it reads is extended inside one refresh | `PinyinSearchPatches.RecipeSimpleNamePatch`; `git diff` of `PlayerCamera.cs` is empty |
| Native list construction (rows, ordering, category filter, scroll, selection index) | Untouched — no row destruction, no re-layout, no private row list | `PinyinSearchPatches` contains no `Object.Destroy` and no reflection on rows |
| Native text-vs-item-filter precedence | Preserved by construction: with the item filter set the scope never opens | `RecipeSearchScope.HasItemFilter` + `RecipeSearchDecision` (code reading; the decision is unit-tested, the native precedence is not) |
| `Recipe.simpleName` outside a search | Untouched (scope closed) — tooltips outside a refresh read the real name | `RecipeSearchScope.Query` is null outside prefix…finalizer |
| Reading table | Vendored whole (26,585 lines) as an embedded resource, fuzzy alternates expanded at load | `PinyinDictionaryTests` census floor > 20,000 |
| Search predicate | Native substring OR pinyin, shared by every surface | `NameSearchMatcher`, `NameSearchMatcherTests` |
| Config → Game Adapter | `IOptionsMonitor` read by a construction-time gate (never a copied bool) | `GameAdapter` ctor `PinyinSearchGate.Bind`; `PinyinSearchGate.Enabled`; the wiring itself is not unit-tested |
| Online UI | Existing Preferences page gained one row; no new page | `OnlineUiPreferencesDrawer.DrawPinyinSearch` |
| Protocol / wire | **Untouched** — protocol stays 34 (a fact, not a design input) | no message type changed |

## Verification design

- **Focused**: `dotnet test CasualtiesUnknownOnline.slnx --filter "FullyQualifiedName~Pinyin|FullyQualifiedName~NameSearchMatcher|FullyQualifiedName~RecipeSearchDecision"` → **57 passed / 0 failed**.
- **Normative gates**: **69 passed / 0 failed**. This run also caught three real violations on the
  way: two top-level types in one file (twice) and `CuoBootstrap` crossing its 600-line aggregate
  cap.
- **Full suite with build**: `CasualtiesUnknownOnline.Tests` **3588 passed / 0 failed** (previous
  cycle 3531 + this change's 57 cases) plus the gates project's 69. `dotnet format` exits 0.
- The matcher tests carry the ticket's own examples (`xianweisheng` / `xws` / `sheng` / `纤wei` /
  `xian维` → 纤维绳) plus counterexamples, the fuzzy sounds in BOTH directions for every pair the
  feature claims (`s`↔`sh`, `z`↔`zh`, `c`↔`ch`, the dropped and the added trailing `g`), polyphonic
  readings (纤 xian1/qian4, 长 zhang3/chang2, 中 zhong1/zhong4), tone tolerance, and the
  ASCII/literal cases that must be unaffected.
- **Two independent adversarial review rounds** (fresh contexts, frozen tree, reviewers wrote nothing
  into the tree; the second round's full report is on disk at
  `%TEMP%\cuo-review-pinyin-stage1.md`):
  - Round 1: 0 blocker / 4 major / 4 minor / 2 nit — mechanism, scope safety, guard and all three
    numbers reproduced independently; the findings were the missing failure signal (a silently
    inert hook) and a user-visible over-claim (the switch text promised the console surface).
  - Round 2 (after the fixes): 0 blocker / 3 major / 5 minor / 4 nit — the two fixes verified, plus
    three findings that are fixed here: the failure signal's wording claimed more than the signal
    covers, this evidence file claimed test coverage the tree does not have, and the fuzzy-sound
    tests did not drive `z↔zh` / `c↔ch` / `in↔ing`. Round 2 also confirmed: predicate semantics
    (`StringComparison` 5 = `OrdinalIgnoreCase`), `"".Contains(query, OrdinalIgnoreCase) == false` on
    .NET Framework, the read-path count/`End` double-call correctness, the dictionary bytes/lines,
    a 32,219-reading comparison against the reference implementation with zero collection drift, and
    6,000 random ASCII queries with zero divergence from the native rule.

## Test coverage split (stated exactly)

- **Covered by tests**: the matcher core, the reading table loading/fuzzy expansion, the shared
  predicate, and the pure decision matrix including the switch-off and item-filter paths.
- **Not covered by any test**: the two patch classes, `RecipeSearchScope`, `PinyinSearchGate`, the
  DI wiring and the BepInEx config binding. The test project excludes the Game Adapter from
  compilation and those types are `internal`, so this half is structurally untestable here; its
  evidence is the patch-contract check (target existence and parameter names) plus the user's
  in-game acceptance.

## What the user must verify (not provable by automation)

- The in-game feel of the native search box with pinyin input: rows appearing and disappearing per
  keystroke, tooltips of the surviving rows, the scroll bar and the "clear" button, and that a
  search made while an item filter ("see recipes with this item") is active stays unfiltered by the
  text box.
- The Preferences toggle row renders, toggles and persists, and the game picks up the new value
  without a restart.
- The default on a Simplified Chinese desktop (the first run writes `PinyinSearch = true`).

## Limits (recorded, not hidden)

- **The read-path signal proves less than "the predicate still works"**: it fires when three
  consecutive refreshes with an active query consulted no recipe name at all, which means either an
  empty recipe list or a game-side change that stopped reading `Recipe.simpleName`. A predicate that
  switched to a *different* name while the row-building loop keeps reading `simpleName` keeps the
  counter fed and stays silent — a documented gap, not a guarded one.
- The reading table is third-party data: 26,585 characters, and a character outside it silently
  degrades to the literal substring rule instead of failing.
- `RecipeSearchScope` reads the private `PlayerCamera.recipeItemFilter` through `AccessTools`. If a
  game update removes that field, the guard refuses to filter (native behavior) and logs one
  warning — deliberately, because guessing would silently drop rows from an item-filter view. The
  probe runs only while the switch is on and a query is typed.
- The English and Chinese tables have no key-set parity gate: the 4 new keys were verified by
  inspection, and a missing key would fall back to English silently.
- `CuoBootstrap` sits exactly on its 600-line aggregate cap (the gate fails only above it): the next
  option registration must split the file. The pinyin default monitor is registered there so every
  composition that resolves the Game Adapter has a value.
- `docs/backlog/README.md`'s row for this ticket does not distinguish the landed stage from the
  pending one; that is the index contract (a row says what a ticket IS, never its history), so the
  progress lives in the ticket itself.
- A query containing `:` has no test (no recipe name in the decompiled tree contains one); under
  `OrdinalIgnoreCase` it is an ordinary character, so the reasoning is equivalence, not evidence.
- Stage 2 (console resource completion) is not implemented; the switch currently affects the
  crafting surface only.
