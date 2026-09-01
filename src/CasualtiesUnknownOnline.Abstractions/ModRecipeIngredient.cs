using System.Runtime.Serialization;

namespace CasualtiesUnknownOnline.Abstractions;

/// <summary>
/// One ingredient requirement inside a <see cref="ModRecipeDefinition"/>.
/// <c>ItemId</c> is a specific item id when non-empty; otherwise the recipe
/// matches by <c>Quality</c> against the item's crafting qualities.
/// </summary>
[DataContract]
public sealed class ModRecipeIngredient
{
	/// <summary>The specific item/liquid id required. Empty when matching by quality.</summary>
	[DataMember(Order = 1)]
	public string ItemId { get; set; } = "";

	/// <summary>True when the required ingredient is a liquid inside a container.</summary>
	[DataMember(Order = 2)]
	public bool IsLiquid { get; set; }

	/// <summary>The crafting-quality id matched when <see cref="ItemId"/> is empty.</summary>
	[DataMember(Order = 3)]
	public string Quality { get; set; } = "";

	/// <summary>The required quality amount.</summary>
	[DataMember(Order = 4)]
	public float QualityAmount { get; set; } = 1f;

	/// <summary>The minimum condition allowed for the matching item.</summary>
	[DataMember(Order = 5)]
	public float MinimumCondition { get; set; } = 0.9f;

	/// <summary>True when the ingredient is consumed/destroyed on craft.</summary>
	[DataMember(Order = 6)]
	public bool DestroyItem { get; set; } = true;
}
