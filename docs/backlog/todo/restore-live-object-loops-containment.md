# The restore's remaining row loops: the live-object tables and the recipe table

- Status: Todo — split out of `review/restored-entity-row-containment.md` (2026-09-17), whose
  independent adversarial pass found the family wider than the three appliers that ticket named
- Priority: Low
- Category: Sync / restore accounting
- Source: the row-containment cycle's adversarial pass (decision 183)
- Related: `docs/decisions/active.md` 183, `docs/backlog/review/restored-entity-row-containment.md`,
  `src/CasualtiesUnknownOnline.GameAdapter/World/KeypadCodeTable.cs`,
  `src/CasualtiesUnknownOnline.GameAdapter/World/GeyserStateTable.cs`,
  `src/CasualtiesUnknownOnline.GameAdapter/World/RecipeUnlockTable.cs`,
  `src/CasualtiesUnknownOnline.Runtime/Session/World/ContainedRowLoop.cs`

## The gap

`ContainedRowLoop` (decision 183) now contains the three world-entity appliers AND the two world-fact
tables that iterate the RESTORED rows into the world's block/damage API (`WorldBlockStateTable.Apply`,
`GameBlockDamageTable.Apply`). Three appliers of the same restore still have no per-row guard, and the
reason each one was left is recorded here rather than folded into the same change:

| applier | its loop | why it was not converted in the same cycle |
|---|---|---|
| `KeypadCodeTable.Apply` / `ApplyAbsolute` | `foreach (var openable in Object.FindObjectsOfType<Openable>())`, counting MATCHED rows | it iterates the LIVE world, not the restored rows: a row has no position in the loop, so "one row costs itself" would have to key on the live object, and the count it would refine is "matched rows", not "written rows" |
| `GeyserStateTable.Apply` | the same shape over `Object.FindObjectsOfType<GeyserScript>()` | same |
| `RecipeUnlockTable.Apply` | `foreach (var row in rows)` writing `hasMadeBefore` / `INT` at an index the loop just validated | the least plausible throw in the family (pure field writes on a validated, non-null recipe), and the record it feeds (`RecipeUnlockApplyResult.RefusedIndexes`) names INDICES — a throwing row would be reported as "this table has no recipe at this index", which is the wrong reason and would have to grow the report's vocabulary |

The blast radius of all three is decision 175's: the throw escapes into the seam that called the
applier, so the rest of that applier's rows and every applier after it are lost, and the account can
say no more than "the live-world write threw".

## Scope

1. Decide the identity and the count each live-object loop can honestly report: the live object's
   position is the only key it has, and "matched rows" must stay the count it reports.
2. Convert `RecipeUnlockTable.Apply` with `ContainedRowLoop.RunContained`, and decide how a throwing
   row is NAMED in the restore report (index, position, or a refused class of its own) — the account
   must stay honest, not merely non-zero.
3. Keep the counts exact: applied plus refused never exceeds the row count, and no row is counted
   twice.

## Acceptance

| # | Scenario | Expected |
|---|---|---|
| 1 | A keypad or geyser write throws mid-loop | The rows after it still land, and the refused count names the one that did not |
| 2 | A recipe row throws | The rows after it still land, and the report names the row and calls it refused |
| 3 | No throw anywhere | Every count is identical to today's |

## Verification limits

The three loops are game-typed (`Object.FindObjectsOfType`, `Traverse`, `Recipes.recipes`), so only the
part that moves into `ContainedRowLoop` is machine-checked; the call sites stay read-only reviewed, the
same limit the parent ticket records.
