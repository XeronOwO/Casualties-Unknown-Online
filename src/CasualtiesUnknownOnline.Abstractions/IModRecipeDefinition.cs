using System.Collections.Generic;

namespace CasualtiesUnknownOnline.Abstractions;

/// <summary>
/// One recipe declaration. This is the contract a consumer reads: the Game
/// Adapter recipe provider materializes the corresponding vanilla
/// <c>Recipe</c> object once the game's recipe table is ready, from these
/// members rather than from a concrete class.
///
/// <see cref="ModRecipeDefinition"/> is the framework's ready-made
/// implementation: use it when every value is a constant, and implement this
/// interface when one is computed.
/// </summary>
public interface IModRecipeDefinition : IModContentDefinition
{
	/// <summary>The item/liquid id produced by the recipe.</summary>
	string ResultItemId { get; }

	/// <summary>True when the result is a liquid (not an item prefab).</summary>
	bool ResultIsLiquid { get; }

	/// <summary>Result stack amount (defaults to 1).</summary>
	int ResultAmount { get; }

	/// <summary>Result condition/amount fraction applied at spawn time.</summary>
	float ResultCondition { get; }

	/// <summary>True when the result should keep its container-liquid contents.</summary>
	bool DontDrainResultLiquid { get; }

	/// <summary>The intelligence requirement for this recipe (0 makes it visible early).</summary>
	int Intelligence { get; }

	/// <summary>The recipe category (see <see cref="ModRecipeCategory"/>).</summary>
	string Category { get; }

	/// <summary>True when this is a repair recipe (the result id is allowed as an ingredient).</summary>
	bool IsRepair { get; }

	/// <summary>The ordered ingredient requirements.</summary>
	List<IModRecipeIngredient> Ingredients { get; }
}
