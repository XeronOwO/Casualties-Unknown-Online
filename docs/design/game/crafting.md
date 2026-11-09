# The game's crafting system (1.0)

Cited from the 1.0 decompile (`reversing/release-1.0/Assembly-CSharp/`).

## A recipe is data, not a script

A `Recipe` holds its result item, its ingredients, an INT requirement and the "made before" flag (`Recipe.cs`). The
name the panel shows is composed from the result's condition: a liquid result shows millilitres, a damaged result shows
a percentage (`Recipe.cs:36`, `:41`); a recipe the player has never made carries the game's own "new" sprite marker
(`Recipe.cs:28`).

- The INT gate is a comparison, not a list: a recipe is visible when the player's Intelligence exceeds the recipe's
  requirement by up to three points (`PlayerCamera.main.body.skills.INT - this.INT >= -3`, `Recipe.cs:102`), unless the
  recipe is flagged known.
- Craftability is a lookup, not a state: the panel asks every visible recipe for `GetItemsForRecipe()` and treats a
  non-null answer as "craftable now" (`PlayerCamera.cs:284-287`). The method walks the inventory for the ingredients and
  returns null when any is missing (`Recipe.cs:107-136`); a "thorough" variant also looks inside containers
  (`Recipe.cs:138`).
- Ingredients are consumed through the game's own `UseItem` per recipe item; `destroyItem` decides whether an
  ingredient is destroyed or merely used (`Recipe.cs:129`, `:172-179`).

## The panel

- The craft window is `PlayerCamera.craftingPanel`, toggled by the `craft` key bind (`Settings.cs:436` binds it to
  `KeyCode.C`; `PlayerCamera.cs:897` toggles it while the filter field is not focused, and `:198-203` animates it).
- The panel lists `Recipes.GetVisibleRecipes(filter)` filtered by the search box; the filter compares the recipe's
  `simpleName` with `Contains(…, OrdinalIgnoreCase)` (`PlayerCamera.cs:222-224`).
- Categories are a `Recipes.RecipeCategory` enum; the panel keeps the selected category and the selected recipe in
  `PlayerCamera` (`PlayerCamera.cs:144`, `:419`).
- The selected recipe's detail block reads the result sprite, its colour and a scaled image
  (`PlayerCamera.cs:419-424`).

## Quality and durability

- A recipe's ingredients may carry a `CraftingQuality` (an id plus an amount, `CraftingQuality.cs`); the item table
  attaches such qualities to tools (for example a "cutting" quality of 200 on a saw, `Item.cs:5524`).
- The result's `resultCondition` is the durability (or the fill) the craft produces; the panel displays it as above.
- A crafted item's condition then decays through the game's own use paths (see `items.md`).

## What the mod must respect

- The panel's "craftable" answer is derived from the inventory every refresh; a mod that changes holdings must let the
  native refresh run rather than caching the answer.
- The recipe list is data (`Recipes.recipes`) — the mod's recursive-crafting layer reads it and adds a marker to the
  panel's own lines, never a parallel list.

## Not covered here

- The full ingredient table and every recipe's numbers: they live in `Recipes.cs`'s data, not read here.
- The tutorial's craft button (`TutorialCraftButtonBehaviour.cs`) and the crafting course (`CraftingCourse.cs`).
