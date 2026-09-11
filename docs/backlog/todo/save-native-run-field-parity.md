# Native run fields are not covered by the world archive

- Status: Todo (decision frozen with the user on 2026-09-10; implementation lands with S3.4 of
  `todo/save-mid-run-consistent-cut.md`)
- Priority: Medium-High
- Category: Persistence / save system
- Source: found by the S2 independent adversarial review (2026-09-10)
- Related: `docs/architecture/save-archive-format.md`, `review/save-layer-end-save-and-restore.md` (S2), `docs/decisions/active.md` 166

## Problem

The frozen v1 scope (decision 166) is the kernel checkpoint + run baseline + character data + world
diff. Several live native fields that shape a layer are in none of them, because no CUO domain owns
them. Native `SaveSystem.TryLoadGame` used to restore them (`reversing/Assembly-CSharp/Assembly-CSharp/SaveSystem.cs:433-445`);
CUO deliberately does not read that file any more (decision 165), and S2 blocks it, so a continued
world starts each of them from a default:

| field | where it is produced | why it matters |
|---|---|---|
| `lootRarityMultiplier` | `WorldGeneration.cs:1061-1062`, accumulated per layer | loot distribution of every later layer differs from the run being continued |
| `trapRarityMultiplier` | same | trap/entity distribution differs |
| `caloriesConsumed` | `PlayerCamera.caloriesConsumed` | a per-run counter the game carries |
| `lastHappiness` | `Body.lastHappiness` | character history |
| `savedRecipeData` | `Recipes.recipes[].hasMadeBefore/INT` | recipe unlock state (also tracked by the kernel backlog item `recipe-unlock-fallback.md`) |
| `savedRunTime` | run clock base | the layer's time-limit accounting |
| `WoundView.cInfo` | native limb-injury carry-over window | presentation of old injuries |

The run baseline's `RunSettings` DO get restored (S2 applies them before `WorldGeneration.Start`
consumes them), so `trapRarityMultiplier`'s per-run setting input is right; what is missing is the
accumulated value itself and the fields with no setting input at all.

Effect: acceptance row 1's "identical character/run state" holds for the kernel/character half and
for the layer's generated content on the FIRST layer of a run, but a restore deeper into a run is
not byte-identical to the interrupted run.

## Decision (frozen with the user, 2026-09-10)

| field | decided home | restore seam |
|---|---|---|
| `lootRarityMultiplier` | `run.json` (world-generation block) | written back where the native `SaveSystem.TryLoadGame` used to run — the arm that currently only skips it — before `WorldGeneration` derives anything from it (`WorldGeneration.cs:253-262`) |
| `trapRarityMultiplier` | `run.json` (world-generation block) | same seam |
| `savedRunTime` | `run.json` | same seam |
| `savedRecipeData` | `run.json` (recipes table) | same seam; a future kernel recipe domain would take it over |
| `lastHappiness` | `characters/<playerKey>.json` | the character-apply path (body-level) |
| `caloriesConsumed` | `characters/<playerKey>.json` | the character-apply path (body-level) |
| `WoundView.cInfo` | `characters/<playerKey>.json` | the character-apply path; kept for native parity — four ints — although no reader besides the save system itself was found |

Evidence for the decision:

- The native write/read pair is `SaveSystem.cs:151-180` (write) and `SaveSystem.cs:433-446` (read).
- Restore timing decides the seam: `WorldParamsService.TryApplyRestoredNow` runs at the Continue
  click, before `WorldGeneration.world` exists, so the multiplier/time fields cannot be written
  there; the patch that skips `SaveSystem.TryLoadGame` runs after the world object exists and before
  its values are consumed, which is the only correct point.
- `savedRecipeData` consumers: `Recipe.cs:28`, `Recipe.cs:183-185`, `PlayerCamera.cs:432`; it is
  reset by `MindwipeScript.cs:79`.
- `WoundView.cInfo` is declared at `WoundView.cs:826` and written by `SetCharDetails` (`:54-65`);
  the only other references are the save system's own write/read.

## Scope

1. Decide per field whether it is (a) kernel-owned state that should move into an existing domain
   (recipe unlocks already have a candidate), (b) a run-baseline field that belongs in `RunState`, or
   (c) genuinely presentation-only and acceptably reset with an explicit in-game note.
2. Persist what is decided into the domain files (§3.4) — no new blob, one file per domain table.
3. Restore it before `WorldGeneration.Start` reads it (the S2 click-time apply is the seam).
4. A field deliberately left out must be named in the restore report, never silently defaulted.

## Acceptance

| # | Scenario | Expected |
|---|---|---|
| 1 | Continue a run at layer 3 whose `trapincrease`/`lootmultiplier` settings were non-default | The next layer's trap/loot distribution matches the interrupted run's |
| 2 | Continue after crafting a recipe in an earlier layer | The recipe stays unlocked |
| 3 | A field decided as presentation-only | The report names it; nothing else changes silently |

## Verification limits

Row 1 needs the user's in-game run: distribution counts are produced by the game's generation and
cannot be asserted from this test host. Rows 2–3 can be machine-verified once the field has a
persisted home.
