# Acceptance — attribute-only declarations materialize, and a computed value re-reads on rebuild

- Ticket: `docs/backlog/done/mod-content-attribute-declarations.md`
- Artifact: `0.1.0+6e782b7a`
- Batch: `.acceptance/20261024-c/` (local, gitignored; run `mc`)
- Date: 2026-10-24
- Result: **every active row passes** (the nested-member row was moved to its own ticket long ago).

## What was run

The built example mod (`CasualtiesUnknownOnline.ModExample.dll`, `cuo.example`) was placed in the deployed
plugins directory and one client started a world. Its declarations are attribute-only (`ExampleDressing`,
`ExampleDressingRecipe` and the tonic classes carry `[ModContent…]` and no registration call); `example.trophy`
is the code-registered half. The HOST was read in-process through the game's own tables:

- `Item.GlobalItems` carries `example.trophy` (present, its own values) and `example.dressing` (present, weight
  `1.5` — the value it computes from the mod's own configuration).
- `Recipes.recipes` (133 rows) carries a row whose `result.id` is `example.dressing` (label
  `<sprite index=25>example.dressing`, one ingredient): the attribute-declared RECIPE materialized.
- The mod's own console command reached the CUO console: `/exampleweight 3.5` answered
  `the declared dressing reports weight 3.5 from the next item-table rebuild`.
- The world was left and a NEW run started (the item-table rebuild); `Item.GlobalItems["example.dressing"]`
  then read weight `3.5`.

## Rows

| Row | Verdict |
|---|---|
| A mod declares an item and a recipe by attribute only, from its own classes, with no registration call, and both materialize in game tables | pass — `example.dressing` in `Item.GlobalItems` and its row in `Recipes.recipes` |
| A declaration whose members COMPUTE their values materializes with them and is re-read when the table is rebuilt | pass — `1.5` → command → new run → `3.5` |
| A mod-authored nested implementation (its own `IModItemTool`) materializes | not this ticket: moved verbatim to `review/mod-content-nested-member-contracts.md` |
| The same definitions registered through code still materialize | pass — `example.trophy` present with its own values |

## Substitutions and limits

- The fixture is the repository's own example mod; the rebuild is a leave-world + new-run (the game's own
  table rebuild), not a process restart.
- `example.tonic` (a liquid-pool example) does not appear as an item, which is correct for its kind; the
  ticket's row names an item and a recipe, both covered by the dressing.
- The example DLL was removed after the run and the deployment re-verified (`0.1.0+6e782b7a`).
- No OS-level mouse or keyboard input was used.
