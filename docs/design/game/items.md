# Items (1.0)

Everything a player carries is an `Item` component (`reversing/release-1.0/Assembly-CSharp/Item.cs:85`). This page records
the rules the item object itself enforces; the containers, wearables and liquids it couples to are named where they
matter.

## Condition

- `condition` is a float the item owns; `SetCondition(float)` is the only writer (`Item.cs:100-108`). It couples to
  liquids: a water container drains proportionally when the old condition is above zero
  (`waterContainerItem.Drain(waterContainerItem.CalculateDrain(capacity * (1 - newCond / oldCond)))`).
- A full-condition item cannot be used in repair recipes (1.0 patchnote "Items with full condition cannot be used in
  repair recipes anymore"); the repair recipe's own check reads the condition, not a durability counter.
- The save stores the condition per item (`SaveSystem.cs` `SavedItem.condition`), so a repair or a cut keeps it.

## Identity and presentation

- `id` is the definition id (`Item.id`), stable across saves; `Stats` is the definition's `ItemInfo` row.
- `label` is the player's own name for the item, set by the item customizer (`PlayerCamera.cs:861`) and persisted
  (`SaveSystem.cs:352`); an empty label means "use the definition's name".
- `colorSwatch` is the player's colour for the item (`PlayerCamera.cs:841`); the customizer also toggles `favourited`
  (`:851`), which the tooltip marks with a sprite (`PlayerCamera.cs:1462-1464`).
- The customizer is opened by `PlayerCamera.OpenItemCustomizer(item)` (`PlayerCamera.cs:90`); the placeholder shows the
  definition's name only when the item is "recognizable" (`item.Stats.rec.recognizable`), otherwise "Something...".

## Liquids

- A container is a `WaterContainerItem` with a capacity and a list of `LiquidStack`s (`LiquidItemInfo`/`LiquidStack`);
  its `Awake` fills the stack from `defaultContents` when empty — this is why a prefab asset's state is not a usable item
  (the prefab's `Awake` never ran).
- `LiquidItemInfo.useLimbAction` is what the mod's family table reads for topical items: `paincream`, `woundglue`,
  `disinfectant`, `spraybottle` call `WaterContainerItem.ApplyToLimb`.
- Wetness: `canGetWet` comes from the `cangetwet` tag; `isWet` is `Time.time - wetTime < 120f` (`Item.cs:75-90`).

## Prosthetics

- A prosthetic is used through an item action that writes limb state: `Item.cs:749-830` raises
  `alertprosthesisbody`/`alertprosthesisarm`/`alertprosthesisleg`, and `alertprosthesisfail` with the failure value; the
  steel prosthetic is created as `steelprosthesis` (`:791`, `:827`). The limb swap itself is a limb operation, not an
  item property.
- A prosthetic limb is repaired by its own minigame (`ProsthesisRepairMinigame.cs`) — the same "hold the hand and follow
  the target" shape as the bandage.

## Per-item special behaviour

- `CustomItemBehaviour` (`CustomItemBehaviour.cs`) is a per-id switch in `Update` (cryogenic core's temperature check,
  autozoom goggles' zoom, and the like). It is not a general scripting layer: a new id with behaviour needs a branch
  there or its own component.

## What this page does not cover

- Container capacity and nesting rules (`Container.cs`), wearable slots (`Wearable.cs`), and the crafting table's item
  categories (`GameCraftingTable.cs`) — named here, read on their own pages.
