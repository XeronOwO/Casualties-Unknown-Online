# Blueprint Popup Sync Self-Check (#195)

Status: RESOLVED (2026-08-18, no protocol bump).
Scope: when a blueprint is used, the recipe-unlock fact (`RecipeUnlockMsg`)
already reaches every side and sets `Recipes.recipes[idx].INT = 0`. The missing
slice was the game's native "learned recipe" popup: only the acting player saw
it. This change shows the same popup on the other sides for a NEW learn and
suppresses the duplicate on the acting side (whose native use action already
showed it).

## Mechanism inventory

| # | Mechanism | Evidence |
|---|---|---|
| 1 | Native blueprint use action | `Item.cs:4279-4298` — sets `recipe.INT = 0`, builds `Locale.GetOther("learnedrecipe").Replace("r1", Locale.GetItem(recipe.simpleName))`, calls `PlayerCamera.main.DoAlert(text, false)` |
| 2 | Recipe-unlock wire | `RecipeUnlockMsg` (NetMsg 77) + `CraftSyncService.SendRecipeUnlock` / `FireRecipeUnlockReceived` — unchanged; every side raises `RecipeUnlockReceived` |
| 3 | Apply shell | `GameAdapter/Items/RecipeUnlockApply.cs` — sets the per-process static `INT` |
| 4 | Start-gate popup deferral | `PlayerCameraDoAlertPatch` + `StartGateAlertQueue` already queue `DoAlert` during the wait window, so a blueprint popup at that edge is safe |

## Change

- Before writing `INT = 0`, capture `newlyUnlocked = ShouldShowPopup(recipe.INT)`.
- After the write, when newly unlocked, show the identical native popup text
  through `PlayerCamera.main.DoAlert` and log it.
- When `PlayerCamera.main` is not available yet, skip the popup with a warning
  (the unlock state itself is still applied).
- Pure helpers `ShouldShowPopup(int previousInt)` and
  `BuildPopupText(template, itemName)` are internal static and reflectively tested.

## Self-check table

| Scenario | Change | Evidence |
|---|---|---|
| Actor's own side (host or guest) | Native use action already showed the popup and set `INT = 0`; apply observes `previousInt == 0` → no duplicate | `Item.cs:4284-4287`; `RecipeUnlockApply.OnRecipeUnlockReceived` pre-write check |
| Other guests receiving the relay | `INT != 0` before apply → popup shown once | `RecipeUnlockApply` new branch |
| Host receiving a guest report | `INT != 0` → popup shown; already learned (`INT == 0`) → skipped | same branch |
| Duplicate / already-learned relay | Skip — no repeated alerts | `ShouldShowPopup(0) == false` |
| Before `PlayerCamera.main` exists | Popup skipped with `[Crafting] ... popup skipped`; unlock still applied | `ShowNewlyUnlockedPopup` null guard (Unity `==`) |
| Text parity | Same template and item-name replacement as native | `BuildPopupText` + `Item.cs:4285-4287` |

## Verification design

- L0: `RecipeUnlockPopupTests` reflectively locks `ShouldShowPopup` (new learn only),
  `BuildPopupText` (placeholder replacement) and the apply/popup method shapes.
- Full suite: `dotnet test CasualtiesUnknownOnline.slnx` — 978 tests green.
- Runtime observability: `[Crafting] recipe {Index} unlocked` always,
  `[Crafting] showed recipe-unlock popup` when shown, `[Crafting] ... popup skipped`
  when the player camera is unavailable.
- No manual acceptance (development-period zero-manual-acceptance rule).