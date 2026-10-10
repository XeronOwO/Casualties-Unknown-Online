namespace CasualtiesUnknownOnline.Abstractions;

/// <summary>
/// One ingredient requirement inside a recipe declaration. This is the contract a
/// consumer reads: <see cref="IModRecipeDefinition.Ingredients"/> is a list of
/// these, so a mod that computes an ingredient hands over its own implementation
/// instead of filling in the framework's.
///
/// <see cref="ModRecipeIngredient"/> is the framework's ready-made implementation:
/// use it when every value is a constant, and implement this interface when one is
/// computed. Partial customisation is composition — an implementation hands back a
/// filled default for the members it does not touch — never inheritance from the
/// data class, which stays <c>sealed</c>.
/// </summary>
public interface IModRecipeIngredient
{
	/// <summary>The specific item/liquid id required. Empty when matching by quality.</summary>
	string ItemId { get; }

	/// <summary>True when the required ingredient is a liquid inside a container.</summary>
	bool IsLiquid { get; }

	/// <summary>The crafting-quality id matched when <see cref="ItemId"/> is empty.</summary>
	string Quality { get; }

	/// <summary>The required quality amount.</summary>
	float QualityAmount { get; }

	/// <summary>The minimum condition allowed for the matching item.</summary>
	float MinimumCondition { get; }

	/// <summary>True when the ingredient is consumed/destroyed on craft.</summary>
	bool DestroyItem { get; }
}
