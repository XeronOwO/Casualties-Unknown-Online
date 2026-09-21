# Pinyin search as a standalone mod (ticket stages 2 and 3)

Date: 2026-09-21
Scope: the pinyin search leaves CUO and becomes a satellite mod that lives in this repository:
`src/CasualtiesUnknownOnline.PinyinSearch/` (game-binding half) plus
`src/CasualtiesUnknownOnline.PinyinSearch.Core/` (game-free half), its own switch, its Tier-2
declaration, and the deletion of CUO's own pinyin implementation (ticket
`docs/backlog/review/pinyin-search-standalone-mod.md`, decision 209). The CUO-resident implementation
this replaces is recorded in `pinyin-search-selfcheck.md` (decision 199) and
`pinyin-console-completion-selfcheck.md` (decision 200) — both are historical as of this cycle.

## What landed

- **Two assemblies, split along the binding** (the reason, not a preference): the plug-in half
  (`PinyinSearchPlugin`, `Patches/PinyinSearchPatches`, `Patches/RecipeSearchScope`) references
  `0Harmony`, `Assembly-CSharp`, `UnityEngine` and **no CUO assembly**; the Core half
  (`Search/` — reading table, `PinyinDictionary`, `PinyinSyllable`, `PinyinMatcher`,
  `NameSearchMatcher`, `RecipeSearchDecision`, `PinyinTableReport`; `PinyinSearchConfig`,
  `PinyinSearchGate`, `PinyinSearchStage`, `PinyinSearchMod`) references `Abstractions` and
  `BepInEx.Core` and **no game assembly**. `InternalsVisibleTo` covers the plug-in half and the test
  project, so the public surface of the Core artifact is the `[CuoMod]` entry point alone.
- **The switch is the mod's own BepInEx config entry** (`General`/`Enabled`), created in the shell's
  Awake, read live by the crafting decision and by the console stage, default on a Simplified Chinese
  desktop. The config description carries English and Simplified Chinese in one line.
- **The declaration**: `[CuoMod("cuo.pinyinsearch", … NetworkMode.ClientOnly, NativeBinding =
  "PlayerCamera.RefreshRecipeList + Recipe.simpleName getter")]` — Tier 2 of
  `docs/api/advanced-modification-policy.md` §1.1, because the crafting half patches the game's code.
- **The console half** registers `PinyinSearchStage` under the id `pinyin` through
  `IModContext.ResourceCompletion` in `Bind`; a refusal is logged through the mod's own log.
- **CUO's own half is deleted**: `Runtime/Search` (six files + the reading table),
  `Runtime/Configuration/PinyinSearchOptions.cs`,
  `Runtime/Session/Content/PinyinResourceLocationMatchStage.cs` and its composition registration, the
  `CuoBootstrap` options line, the Game Adapter's three patch classes with their `GameAdapter`
  bind/unbind, the `pinyin-search` capability id and catalog row, the plug-in's
  `PinyinSearchConfigEditor`, the Online UI Preferences row and its wiring, the `Search.PinyinSearch`
  entry, and the three localization keys in both tables.

## Mechanism inventory

| Mechanism | Change | Evidence |
|---|---|---|
| Native crafting search box | **Unchanged mechanism, new owner**: the one-refresh borrowed-name scope and the `Recipe.get_simpleName` hook moved to the mod verbatim (minus the CUO DI gate) | `Patches/PinyinSearchPatches.cs`, `Patches/RecipeSearchScope.cs`; behaviour pinned by the moved `PinyinMatcherTests` / `NameSearchMatcherTests` / `RecipeSearchDecisionTests` |
| The switch | CUO's `Search.PinyinSearch` + `IOptionsMonitor<PinyinSearchOptions>` replaced by the mod's own `ConfigEntry<bool>`, read live | `PinyinSearchConfig.cs`; `PinyinSearchStageTests.Suggest_FollowsTheSwitchWithoutRebuildingTheCatalog` |
| Console completion | Providers unchanged (CUO keeps the seam); the stage moved from `Runtime` to the mod and is registered by the mod's own `[CuoMod]` type | `PinyinSearchModConsoleTests.Suggest_ReachesTheCanonicalId_FromPinyinInitialsChineseAndIdPrefixes` (production composition → loaded mod context → catalog → console suggestions) |
| CUO-less operation | Structural: only `PinyinSearchMod` and `PinyinSearchStage` name an `Abstractions` type; the plug-in assembly references no CUO framework assembly | `PinyinSearchContractTests.PluginAssembly_ReferencesNoCuoFrameworkAssembly` and `…CoreAssembly_OnlyTheCuoFacingTypesNameAnAbstractionsType` (metadata walk over the built assemblies, including method-body references) |
| Declaration (Tier 2) | `NativeBinding` + `NetworkMode.ClientOnly` on the `[CuoMod]` attribute | `PinyinSearchContractTests.ModDeclaration_DeclaresTheNativeBindingAndALocalNetworkMode`, plus `…ModEntryPoint_IsAcceptedByCuoDiscovery` (the real `ModRegistry` accepts the entry point with exactly that manifest) |
| Game-update churn | Moved to the mod: the patch targets and the private `recipeItemFilter` field the guard reads are asserted where the mod lives (CUO's capability probe for it was deleted with the row) | `PinyinSearchContractTests.CraftingPatchTargets_StillResolveInTheGameAssembly` |
| CUO's pinyin option/UI/localization | Deleted, not deprecated (pre-release policy: no compatibility layers) | `git show --stat` of this cycle; the option type/key/row/keys are gone from `src/` |

## Verification

- `dotnet build CasualtiesUnknownOnline.slnx` — 0 warnings / 0 errors.
- `dotnet format CasualtiesUnknownOnline.slnx` — exit 0.
- `dotnet test CasualtiesUnknownOnline.slnx` (build included) — gates 84/84, tests 3 747/3 747.
- `dotnet test … --filter "FullyQualifiedName~PinyinSearch"` — 83/83 (the moved matcher families plus
  the stage, console-path and contract tests).
- Subset census re-measured for `test-parallelization.md` §9: 259 classes / 1 691 cases tagged,
  2 056 untagged.

## What this does NOT prove

The native search box as the player experiences it — typing pinyin and watching the recipe list
filter — and the two-install matrix (mod alone / mod + CUO) are **user acceptance**: no automated
test in this repository runs the game's UI. Nothing here claims otherwise; the evidence above is
build, gates, tests, and metadata assertions about the shipped assemblies.

Two limits the predecessor page recorded are carried forward rather than dropped: **the
Simplified-Chinese default is not covered by a test** (`Application.systemLanguage` is read in the
plug-in's `Awake`, and BepInEx writes the default only when the config file is first created — a real
first run on a zh-CN desktop is the only proof), and **Harmony patch application is never executed
here** (it needs `PlayerCamera`/`Recipe`, so `CraftingPatchTargets_StillResolveInTheGameAssembly`
proves the members exist, not that the borrowed-name trick still yields the right rows). The mod also
has **no deployment/identity-verification path in `tools/`** — it is installed by hand (its two DLLs,
plus the transitive `CasualtiesUnknownOnline.Abstractions.dll` the build copies beside them, which
CUO already provides when it is installed) — so no deployed-artifact hash check backs this cycle.
