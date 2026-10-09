namespace CasualtiesUnknownOnline.Abstractions;

/// <summary>
/// One ingredient requirement inside a <see cref="ModRecipeDefinition"/>.
/// <c>ItemId</c> is a specific item id when non-empty; otherwise the recipe
/// matches by <c>Quality</c> against the item's crafting qualities.
/// </summary>
public sealed class ModRecipeIngredient
{
	/// <summary>The specific item/liquid id required. Empty when matching by quality.</summary>
	public string ItemId { get; set; } = "";

	/// <summary>True when the required ingredient is a liquid inside a container.</summary>
	public bool IsLiquid { get; set; }

	/// <summary>The crafting-quality id matched when <see cref="ItemId"/> is empty.</summary>
	public string Quality { get; set; } = "";

	/// <summary>The required quality amount.</summary>
	public float QualityAmount { get; set; } = 1f;

	/// <summary>The minimum condition allowed for the matching item.</summary>
	public float MinimumCondition { get; set; } = 0.9f;

	/// <summary>True when the ingredient is consumed/destroyed on craft.</summary>
	public bool DestroyItem { get; set; } = true;
}
