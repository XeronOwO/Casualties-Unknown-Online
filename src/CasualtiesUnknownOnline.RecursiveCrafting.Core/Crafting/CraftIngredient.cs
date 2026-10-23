namespace CasualtiesUnknownOnline.RecursiveCrafting.Core.Crafting;

/// <summary>
/// One ingredient line of a recipe as the resolver reads it: an item id (the game's own), an optional quality
/// the ingredient must carry, how many units the line needs, and the total condition those units must still
/// have. The condition is the "two into one" rule made concrete: two half-spent items of the same id satisfy a
/// line that asks for one unit at condition 1, because what the line consumes is their remaining condition, not
/// their count.
/// </summary>
public sealed class CraftIngredient(string itemId, int amount = 1, string? quality = null, float totalCondition = 1f)
{
	/// <summary>The game's item id this line asks for.</summary>
	public string ItemId { get; } = itemId;

	/// <summary>How many units of the id the line consumes.</summary>
	public int Amount { get; } = amount;

	/// <summary>The quality the item must carry, or null for any quality (including none).</summary>
	public string? Quality { get; } = quality;

	/// <summary>The summed remaining condition the consumed units must still carry.</summary>
	public float TotalCondition { get; } = totalCondition;

	/// <summary>A fresh line: one unit at full condition.</summary>
	public static CraftIngredient Fresh(string itemId) => new(itemId);
}
