# Pinyin search in the native crafting box — machine reading (2026-11-08)

The row: pinyin search narrows the game's own crafting list when the player types pinyin into the native search
box. Batch `20261108-w`/`-x` on the host desktop (the only client needed — the mod is Tier 2, `NetworkMode.ClientOnly`).

## Fixture

- Deployed artifact `0.1.0+cbf28546` plus the rebuilt `CasualtiesUnknownOnline.PinyinSearch.dll` copied into
  `BepInEx/plugins` (the deploy script does not carry this satellite mod — a named limit below).
- Host in a world; `crafting-search query=<q>` opens the game's own craft screen, writes `<q>` into `recipeFilterField`
  and `PlayerCamera.recipeFilter`, then runs the game's own `RefreshRecipeList`; `rowCount` is
  `PlayerCamera.recipeObjects.Count`.

## Readings

| query | rows | evidence |
|---|---|---|
| `cuiruo` | 1 | log: `[Pinyin] extended match: 'cuiruo' matched '<the fragile knife>'` (the recipe's own name translated) |
| `cui` | 2 | log: `… matched '<the fragile knife>'`, `… matched '<drooping-wheat flour>'` |
| `knife` (the literal query, translated) | 2 | log: `… matched '<the fragile knife>'`, `… matched '<crude kitchen knife>'` |
| `flimsy` (the English id) | 0 | no extended match; the name is not an id |

The field crop `pinyin-search-field-crop.png` (from `pinyin-search-20261108.png`, batch `20261108-x`) shows the native
box holding `cuiruo`; the list behind it is partly covered by the CUO overlay window, so the row identities above come
from the mod's own log lines rather than from the frame.

## What it settles

- The native search box matches a Chinese recipe name by full pinyin (`cuiruo` → the fragile knife), by a partial
  syllable (`cui` → the fragile knife and the drooping-wheat flour), and the literal rule still applies (`knife` → the two
  knife recipes).
- A word that matches neither (`flimsy`) narrows to none — the extension adds matches, it does not bypass the filter.

## The three dead shapes this reading retired

1. `PatchAll(typeof(PinyinSearchPatches))` applied **no patch at all** (Harmony's registry: 246 patched methods, none
   of them `RefreshRecipeList`), while the mod logged success. The install scans the assembly now.
2. A postfix on `Recipe.get_simpleName` can only REMOVE rows: the native predicate still runs its own `Contains` on
   the returned name, so a pinyin-only hit was dropped again.
3. A transpiler on `RefreshRecipeList` found no comparison call — the call lives in the compiler-generated lambda.
   The live extension is a prefix on `string.Contains(string, StringComparison)` inside the one-refresh scope.

## Limits

- `tools/deploy.ps1` does not install this mod (it lives directly in `BepInEx/plugins`); the rebuilt pair of
  assemblies was copied by hand for this batch. Making the deploy carry it is its own change.
- The frame's list area is occluded by the CUO overlay; the count and the matched names are the log's, not pixels.
- One client only: a client-only mod has no cross-client half to judge.
