namespace CasualtiesUnknownOnline.RecursiveCrafting.Core.Crafting;

/// <summary>
/// One ingredient line of a recipe as the resolver reads it, mirroring the game's own two shapes: a SPECIFIC
/// line names an item id, and a TAG line names a crafting quality (the game's "foliage", "sharp" …) any item
/// carrying that quality may satisfy. Both carry how many units the line consumes and the total condition those
/// units must still have — the "two into one" rule made concrete: two half-spent items of the same kind satisfy
/// a line that asks for one unit at condition 1, because what the line consumes is their remaining condition,
/// not their count.
/// </summary>
public sealed class CraftIngredient(string? itemId, int amount = 1, string? quality = null, float totalCondition = 1f)
{
	/// <summary>The game's item id a SPECIFIC line asks for; null for a tag line.</summary>
	public string? ItemId { get; } = itemId;

	/// <summary>How many units of the id the line consumes.</summary>
	public int Amount { get; } = amount;

	/// <summary>The crafting quality a TAG line asks for, or null for a specific line.</summary>
	public string? Quality { get; } = quality;

	/// <summary>The summed remaining condition the consumed units must still carry.</summary>
	public float TotalCondition { get; } = totalCondition;

	/// <summary>A fresh specific line: one unit at full condition.</summary>
	public static CraftIngredient Fresh(string itemId) => new(itemId);

	/// <summary>A tag line: units carrying <paramref name="quality"/> worth <paramref name="totalCondition"/> together.</summary>
	public static CraftIngredient OfQuality(string quality, float totalCondition = 1f, int amount = 1) => new(null, amount, quality, totalCondition);
}
