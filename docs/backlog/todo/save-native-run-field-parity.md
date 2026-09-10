# Native run fields are not covered by the world archive

- Status: Todo
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
