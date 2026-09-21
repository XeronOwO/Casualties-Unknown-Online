# Pinyin search as a standalone mod

- Status: Todo
- Priority: Medium
- Category: Mod / Tooling / UI / Pinyin
- Depends on: [Native-binding mods declare it instead of hiding](../review/mod-native-binding-declaration.md)
  for the crafting half's tier, and on the Stage-1 seam below for the console half.

## Why this exists

Both stages of the pinyin search landed inside CUO (decisions 199 and 200; the landed ticket is
`docs/backlog/review/pinyin-search-mod.md`): the matcher core and the console stage in `Runtime`,
the crafting-box patch in `GameAdapter`, the switch in the plug-in's own config. It works — and it
sits in the wrong place.

The owner's ruling (2026-09-20, decision 204): pinyin search is a game quality-of-life feature, not
multiplayer functionality, so it belongs to its own mod — and a mod that reuses the game's own
search box, vendors a 26k-character reading table and ships its own switch is also the most useful
worked example this ecosystem can offer. The tiered model is what makes that possible: the crafting
half binds the game's own code **by declaration**, and the console half needs a seam CUO publishes.

## What moves, what stays

- **Moves out** (the standalone mod owns it): the reading table and `PinyinDictionary` /
  `PinyinSyllable` / `PinyinMatcher`, the shared name predicate, the crafting-box patch
  (`PinyinSearchPatches`, `RecipeSearchScope`, `PinyinSearchGate`), the switch and its UI, and the
  reading-table report.
- **Stays in CUO**: the resource-completion seam — `IResourceLocationCatalog` and the extra
  match-stage contract are CUO's own console vocabulary, and the console itself exists only with
  CUO.
- **Is deleted, not kept** (pre-release policy: no compatibility layers): CUO's own pinyin option
  (`Search.PinyinSearch`), its Preferences row, its localization keys, and the `Runtime/Search` core
  once the mod owns it.

## Stages

### Stage 1 — publish the completion seam (CUO-side, small)

Promote the console's completion extension point to the mod surface: an `Abstractions`-level,
`Experimental` contract that lets a mod register an extra resource-completion stage, plus its
baseline line. This is the promotion funnel working as designed
(`docs/api/advanced-modification-policy.md` §6) with one real consumer.

### Stage 2 — the standalone mod

The mod owns the matcher core, the crafting patch (bound by declaration) and the switch, and works
with CUO absent.

### Stage 3 — the console half and the clean removal

With CUO present, the mod registers its stage through the Stage-1 seam: one mod, one switch, both
surfaces. CUO's own pinyin code, option and UI are deleted in the same cycle, and the localization
keys move to the mod.

## Acceptance

- Crafting search matches pinyin with CUO absent (the mod alone), with the behaviour the current
  tests pin (`xianweisheng` / `xws` / `sheng` / `纤wei` / `xian维` → 纤维绳).
- With CUO present, `fent` / `ftn` / `芬太` / `cu:fent` still complete the console to `cu:fentanyl`
  and the accepted suggestion is still the canonical id — the row to port is
  `PinyinResourceLocationMatchStageTests.Suggest_ReachesTheCanonicalId_FromPinyinInitialsChineseAndIdPrefixes`.
- One switch, owned by the mod, controls both surfaces; CUO exposes no pinyin option of its own.
- The install story is stated in one line: what a Chinese player installs, and what happens when
  both are installed.

## Open questions for the owner

- **Where the mod lives**: upgrade the existing standalone project (`JustUnknownCharacters`, the
  reference implementation this work was ported from) or start a new one? Two pinyin mods in the
  wild would be worse than either answer; the recommendation is to make the existing project the
  one once the ecosystem-facing name is decided.
- **Where the switch lives**: the mod's own BepInEx config (recommended, since CUO no longer owns
  it) — CUO's Preferences page loses the row.

## Limits

- The game-update churn of the crafting patch moves to the mod — the trade the tiered model makes
  explicit: a game update that renames the search predicate breaks the mod, not CUO.
- Until Stage 3 lands, a CUO-less install has the crafting half only, because the console is CUO's.
