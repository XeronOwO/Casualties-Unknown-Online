# Pinyin search as a standalone mod

- Status: Review
- Priority: Medium
- Category: Mod / Tooling / UI / Pinyin
- Depends on: [Native-binding mods declare it instead of hiding](mod-native-binding-declaration.md)
  for the crafting half's tier (landed), and on the Stage-1 seam below for the console half (landed).

## Why this exists

Both stages of the pinyin search landed inside CUO (decisions 199 and 200; the landed ticket is
`pinyin-search-mod.md`): the matcher core and the console stage in `Runtime`, the crafting-box patch
in `GameAdapter`, the switch in the plug-in's own config. It works — and it sits in the wrong place.

The owner's ruling (2026-09-20, decision 204): pinyin search is a game quality-of-life feature, not
multiplayer functionality, so it belongs to its own mod — and a mod that reuses the game's own
search box, vendors a 26k-character reading table and ships its own switch is also the most useful
worked example this ecosystem can offer. The tiered model is what makes that possible: the crafting
half binds the game's own code **by declaration**, and the console half needs a seam CUO publishes.

## What moved, what stayed

- **Moved out** (the standalone mod owns it): the reading table and `PinyinDictionary` /
  `PinyinSyllable` / `PinyinMatcher`, the shared name predicate (`NameSearchMatcher`), the crafting
  decision and the crafting-box patch (`RecipeSearchDecision`, `PinyinSearchPatches`,
  `RecipeSearchScope`), the switch and its config surface, and the reading-table report.
- **Stayed in CUO**: the resource-completion seam — `IResourceLocationCatalog` and the extra
  match-stage contract are CUO's own console vocabulary, and the console itself exists only with CUO.
- **Deleted, not kept** (pre-release policy: no compatibility layers): CUO's own pinyin option
  (`Search.PinyinSearch`), its Preferences row, its localization keys, and the `Runtime/Search` core.

## Stages

### Stage 1 — publish the completion seam (CUO-side, small) — landed 2026-09-21

Promote the console's completion extension point to the mod surface: an `Abstractions`-level,
`Experimental` contract that lets a mod register an extra resource-completion stage, plus its
baseline line. This is the promotion funnel working as designed
(`docs/api/advanced-modification-policy.md` §6) with one real consumer.

### Stage 2 — the standalone mod — landed 2026-09-21

The mod owns the matcher core, the crafting patch (bound by declaration) and the switch, and works
with CUO absent.

### Stage 3 — the console half and the clean removal — landed 2026-09-21

With CUO present, the mod registers its stage through the Stage-1 seam: one mod, one switch, both
surfaces. CUO's own pinyin code, option and UI are deleted in the same cycle.

## What landed (stage 1, 2026-09-21)

The seam, its registry, its tests and its documentation. Stage 1 was landed on its own because it is
the CUO-side half the mod registers through.

- **The contract.** `IResourceLocationMatchStage.Matches(ResourceLocationEntry, string)`,
  `ResourceLocationEntry` (canonical id / kind / display name) and `IModResourceCompletion` live in
  `CasualtiesUnknownOnline.Abstractions` and carry
  `[ApiStability(ApiStabilityLevel.Experimental)]`; `IModContext.ResourceCompletion` is the
  registration entry point. `IResourceLocationMatchStage` and `ResourceLocationEntry` used to live in
  `Runtime/Session/Content` — the promotion is a move plus the new registry, not a second copy.
- **The registry.** The mod-visible surface is `IModResourceCompletion`
  (`IModContext.ResourceCompletion`); `Runtime/Session/Mods/ModResourceCompletionAdapter.cs` is its
  per-mod implementation — it scopes the stage table to one mod id, validates the id (non-blank, at
  most 128 characters), caps a mod at 8 stages and logs every refusal. The stages land in
  `ModResourceCompletionStore` and `ResourceLocationCatalog` reads them, ranking mod stages after the
  framework's own in registration order. The store is the indirection that keeps the mod domain from
  depending on the catalog (the catalog already reaches the mod service through its content source, so
  a direct edge closes a dependency cycle). A query ranks the stages registered when it started, so a
  stage that registers or unregisters during a query changes the next query, not the running one.
- **Isolation.** A throwing stage is contained by the catalog: the entry counts as "no match", the
  later stages still run, and the failure is debug-logged because the path runs per keystroke.
- **Tests.** `tests/.../Mods/ModResourceCompletionTests.cs` drives two stub mods over one catalog
  (scope isolation, the id rails, the cap and the freed slot);
  `tests/.../Mods/ModResourceCompletionConsoleTests.cs` takes a stage from a loaded mod and drives the
  console's argument-suggestion endpoint with it (the production path end to end). The catalog tests
  carry the throwing-stage cases and the reentrancy case (a stage that registers during a query sees
  the next query, not the running one), and `StubMatchStage` / `StubResourceSource` are the shared
  stubs.
- **Docs.** `docs/api/mod-api.md` §4l and the §3 member table; the Abstractions baseline records the
  new types and members.

## What landed (stages 2 and 3, 2026-09-21)

**Where the mod lives (owner ruling, 2026-09-21).** A new pair of projects **in this repository** —
`src/CasualtiesUnknownOnline.PinyinSearch/` and its game-free half
`src/CasualtiesUnknownOnline.PinyinSearch.Core/` — added to `CasualtiesUnknownOnline.slnx`. The
owner's call was "in the CUO directory for now, its own project once it grows"; the naming therefore
keeps this repository's prefix, and extracting the mod later is a folder move plus a rename, not a
rewrite.

- **Why two assemblies.** The split is along the binding, not the feature, and it mirrors CUO's own
  `Runtime` / `GameAdapter` line: the **plug-in half** (BepInEx shell, Harmony patch, Unity types)
  references the game assemblies and **no CUO assembly at all**, while the **Core half** (reading
  table, matcher, name predicate, crafting decision, switch, console stage, `[CuoMod]` entry point)
  references `Abstractions` and no game assembly. That is what makes "works with CUO uninstalled"
  structural rather than a promise: the `[CuoMod]` type and the stage are the only two types in the
  Core assembly that name an `Abstractions` type, so with CUO absent the crafting path loads and runs
  and the two CUO-facing types are simply never touched. Both facts are asserted against the shipped
  assemblies by `PinyinSearchContractTests`.
- **The switch.** One `ConfigEntry<bool>` owned by the mod
  (`BepInEx/config/CasualtiesUnknownOnline.PinyinSearch.cfg`, section `General`, key `Enabled`),
  created by the plug-in shell and read LIVE by both surfaces — the crafting patch's decision and the
  console stage — so one edit covers both without a restart. The default still follows the owner's
  2026-09-20 shape: a Simplified Chinese desktop gets pinyin search, every other system language
  keeps the native search untouched.
- **The declaration.** `[CuoMod("cuo.pinyinsearch", "Pinyin Search", "0.1.0", NetworkMode =
  NetworkMode.ClientOnly, NativeBinding = "PlayerCamera.RefreshRecipeList + Recipe.simpleName
  getter")]` — Tier 2 of `docs/api/advanced-modification-policy.md` §1.1, because the crafting half
  patches the game's own code. `ClientOnly` is the local-UI mode: the search box and the console
  completion exist on the client the player is typing on, and the handshake admits members that do not
  have the mod. The declaration's sentence and the mod's own documentation are the same claim.
- **The console half.** `PinyinSearchMod.Bind` registers `PinyinSearchStage` through
  `IModContext.ResourceCompletion` under the id `pinyin`; a refused registration is logged through the
  mod's own log as well, because the crafting half would otherwise look fine while the console stayed
  silent. The stage keeps the landed semantics: built-in ranks first, additive only, display name only,
  switch-off means no match and no reading-table load.
- **The removal.** CUO's own half is gone in the same cycle: `Runtime/Search` (six files plus the
  reading table), `Runtime/Configuration/PinyinSearchOptions.cs`,
  `Runtime/Session/Content/PinyinResourceLocationMatchStage.cs` and its
  `ContentVocabularyComposition` registration, the `CuoBootstrap` options line, the Game Adapter's
  three patch classes plus its `GameAdapter` bind/unbind and its capability-catalog row
  (`pinyin-search`), the plug-in's `PinyinSearchConfigEditor`, the Online UI's Preferences row and its
  wiring, the `Search.PinyinSearch` config entry, and the three localization keys in both languages.
  The capability catalog gate and the patch-contract tool parity gate recompute from the assembly, so
  both followed the deletion without a manual row.
- **Deviation from the plan, recorded.** The ticket's stage-3 wording said the localization keys "move
  to the mod". They were deleted instead: the switch is the mod's own BepInEx config file, so there is
  no in-game row left to localize, and the config entry's description carries both languages
  (English + Simplified Chinese) in one line rather than shipping a localization system for one
  sentence.
- **Tests.** `tests/.../PinyinSearch/` now holds the mod's surface: the matcher/dictionary/name
  predicate/crafting-decision families moved with the code (same rows), `PinyinSearchStageTests`
  covers the stage, the mod's switch, the live-switch path and the boundaries,
  `PinyinSearchModConsoleTests` drives the real `[CuoMod]` entry point over a loaded mod's context into
  the console's suggestion endpoint (the production path), and `PinyinSearchContractTests` asserts the
  packaging facts above plus the game-side contract the patch binds (`PlayerCamera.RefreshRecipeList`,
  `Recipe.simpleName`, the private `PlayerCamera.recipeItemFilter` the item-filter guard reads — the
  probe CUO's capability catalog used to carry).

## Acceptance

- Crafting search matches pinyin with CUO absent (the mod alone), with the behaviour the moved tests
  pin (`xianweisheng` / `xws` / `sheng` / `纤wei` / `xian维` → 纤维绳): `PinyinMatcherTests`,
  `NameSearchMatcherTests` and `RecipeSearchDecisionTests` carry the rows, and
  `PinyinSearchContractTests.PluginAssembly_ReferencesNoCuoFrameworkAssembly` plus
  `...CoreAssembly_OnlyTheCuoFacingTypesNameAnAbstractionsType` make the CUO-less load path structural.
  **The native search box itself is user acceptance** — no automated test in this repository runs the
  game's UI.
- With CUO present, `fent` / `ftn` / `芬太` / `cu:fent` complete the console to `cu:fentanyl` and the
  accepted suggestion is the canonical id:
  `PinyinSearchModConsoleTests.Suggest_ReachesTheCanonicalId_FromPinyinInitialsChineseAndIdPrefixes`
  (the row ported from the removed `PinyinResourceLocationMatchStageTests`), with the stage-level
  matrix in `PinyinSearchStageTests`.
- One switch, owned by the mod, controls both surfaces; CUO exposes no pinyin option of its own —
  `PinyinSearchStageTests.Suggest_FollowsTheSwitchWithoutRebuildingTheCatalog` and
  `PinyinSearchModConsoleTests.Suggest_SwitchOff_KeepsTheNativeRanks`, and the option's absence is a
  compile-level fact (the type, the config key, the Preferences row and the localization keys are
  deleted).
- CUO's discovery accepts the mod's entry point, with the declaration it reports:
  `PinyinSearchContractTests.ModEntryPoint_IsAcceptedByCuoDiscovery` feeds the Core assembly to the
  real `ModRegistry` and asserts the manifest (`cuo.pinyinsearch`, `ClientOnly`, no permissions, the
  native binding).
- The install story is stated in one line: put `CasualtiesUnknownOnline.PinyinSearch.dll` and
  `CasualtiesUnknownOnline.PinyinSearch.Core.dll` into `BepInEx/plugins`; the crafting search box
  matches pinyin on its own, and with CUO installed the console's resource-id completion does too,
  behind the same switch. (The build output also carries a transitive
  `CasualtiesUnknownOnline.Abstractions.dll`; with CUO installed it is already there, and without CUO
  nothing on the crafting path needs it.)

## Limits

- The game-update churn of the crafting patch belongs to the mod now. The trade is deliberate and
  paid where the mod lives: `PinyinSearchContractTests.CraftingPatchTargets_StillResolveInTheGameAssembly`
  is the mod's own contract probe, and CUO's capability catalog no longer carries the row.
- The scratch-read-path residue CUO recorded (`RecipeSearchScope.ReportSilentReadPath`) is unchanged:
  a native predicate that switched to a different name while the row-building loop kept reading
  `simpleName` would keep the counter fed, so the warning states a possibility, not a verdict.
- The in-game behaviour — the native search box filtering as the player types, and the two-install
  matrix (mod alone / mod + CUO) — is user acceptance territory; this cycle's evidence is build,
  gates, tests and the packaging assertions listed above.
- **Not covered by any test**: Harmony patch application itself (it needs the game types), the
  Simplified-Chinese default on a real first run (BepInEx writes a default only when the config file
  is created), and the mod's deployment — `tools/deploy.ps1` / `verify-deploy.ps1` know the CUO
  plug-in only, so this artifact is installed by hand and has no deployed-hash check. If the mod joins
  the release cycle, it needs the same identity check the plug-in gets.
- The older published standalone mod (`JustUnknownCharacters`) still exists outside this repository.
  Its replacement was not part of this cycle: this ticket makes the in-repo mod the one this
  repository builds and tests, and retiring or updating the published copy is the owner's call.
