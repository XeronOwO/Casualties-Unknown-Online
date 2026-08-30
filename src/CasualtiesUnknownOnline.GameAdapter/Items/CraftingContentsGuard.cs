namespace CasualtiesUnknownOnline.GameAdapter.Items;

/// <summary>
/// Crafting-content guard: a recipe must not consume a destroyable material
/// that still carries contents. The game's destroy path either spills
/// container children into the world (<c>Container.UnloadAllItems</c>),
/// spawns a battery into the inventory (<c>BatteryItem.UnloadBattery</c>), or
/// simply loses a liquid stack when a container item is destroyed. Excluding
/// such materials from a craft is the simpler player-intuitive fix: empty the
/// item before using it as a recipe ingredient.
/// </summary>
internal static class CraftingContentsGuard
{
	internal static bool ShouldRefuse(RecipeItem recipeItem, Item item)
	{
		return ShouldRefuse(
			recipeItem.destroyItem,
			recipeItem.isLiquid,
			HasBattery(item),
			HasContainerChildren(item),
			HasLiquidStack(item));
	}

	/// <summary>
	/// Pure decision surface (testable without a live Unity scene): a craft is
	/// refused only when the material would be destroyed and the recipe is not
	/// a liquid-drain operation, and the item currently carries any of the
	/// content families the destroy path would drop/lose.
	/// </summary>
	internal static bool ShouldRefuse(bool destroyItem, bool isLiquid, bool hasBattery, bool hasContainerChildren, bool hasLiquidStack) =>
		destroyItem && !isLiquid && (hasBattery || hasContainerChildren || hasLiquidStack);

	private static bool HasBattery(Item item)
	{
		var battery = item.battery;
		return battery != null && battery.hasBattery; // Unity object — ==
	}

	private static bool HasContainerChildren(Item item)
	{
		var container = item.GetComponent<Container>();
		return container != null && container.itemCount > 0; // Unity object — ==
	}

	private static bool HasLiquidStack(Item item)
	{
		var water = item.GetComponent<WaterContainerItem>();
		return water != null && water.stack.Count > 0; // Unity object — ==
	}
}
