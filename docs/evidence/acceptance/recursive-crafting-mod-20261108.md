# Recursive crafting — the third state read on the game's own panel (batch 20261108)

- Built: 0.1.0+9869d734 (the acceptance channel present; no `src/` change in this cycle).
- Clients: the host in world `w-20261108-709f` (the three clients were already up from the save-field batch).
- Instrument: the new recipe `tools/acceptance/recipes/crafting-panel.cs` (`mode=open|read|close`, `index=n`), which opens the game's own craft screen through `PlayerCamera.OpenCraftScreen`, selects a recipe, calls the panel's own `RefreshCurrentlySelectedRecipe`, and reads the two lines the panel renders: child 5 (the TextMeshProUGUI the panel assigns `recipe.fullName` into, `PlayerCamera.cs:347`) and child 8 (the ingredient text); `mode=read` also reads the selected recipe's `description` property (the getter the mod patches).
- The fixture's holdings (the third state needs the entry NOT craftable now but reachable through a craft): two scrapmetal stacks at condition 0.5 each (the mod sums the line's consumed condition, so together they satisfy the game's `>= 0.9` line), a woodcube, two foliage, and no rope — the rope was dropped into the world through the game's own release (`drag-from-slot` + `drag-release`, `mode World`, `after none`).

## Read on the live client

| query | answer |
|---|---|
| `/craftchain flimsyknife` (before the fixture) | `cannot be crafted — nothing here provides "scrapmetal".` |
| after the second scrapmetal (both 0.5) | `cannot be crafted — nothing here provides "woodcube".` |
| after woodcube, two foliage, rope dropped | `reachable through 1 step(s) — 1. rope.` |
| the panel's detail name line (`crafting-panel mode=open`) | `脆弱的刀 (+)` ("the fragile knife (+)", translated) |
| the selected recipe's description tail | `… Reachable through other crafts: 1. rope` |
| the frame crop (`craft-panel-row-20261108.png`, header crop) | the panel's own name line renders `脆弱的刀 (+)` beside the result icon |

## What this settles

- The third state is on screen: the panel's own name line carries ` (+)` on the recursive entry, and the recipe's description (the tooltip source) carries the steps line — the two surfaces the stage-3 design named, read from the game's own rendered text and from a frame crop, not from the mod's own getters.
- The row's colour question is settled by construction: the mod adds no colour to the row; the name line keeps the panel's native rendering (the crop shows the native grey/white text).
- The marker appears only for the recursive entry: the same panel with the rope held read `flimsyknife: craftable now` and the detail name carried no marker (the native state), so the third state does not fire when the entry is craftable.

## Limits

- The crop was taken behind the CUO overlay panel (the plugin menu was open); the name line is fully visible, the recipe list behind it is partly covered. The list-row rendering is the native one and was already read in stage 3.
- One recipe (`flimsyknife`) and one category were driven; the caption "reads well" is a product judgement of the same crop, not a separate machine reading.

## Cleanup

- The dropped rope stays in the world (the fixture's own world); no client was restarted, no process was stopped.
