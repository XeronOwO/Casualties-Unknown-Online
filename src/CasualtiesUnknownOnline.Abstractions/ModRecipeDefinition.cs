using System.Collections.Generic;

namespace CasualtiesUnknownOnline.Abstractions;

/// <summary>
/// The mod-authored definition of one recipe content definition. It is a plain
/// DTO in Abstractions: no game assembly, no Unity type, no Runtime dependency.
/// The Game Adapter recipe provider reads it and materializes the corresponding
/// vanilla <c>Recipe</c> object once the game's recipe table is ready.
/// </summary>
public sealed class ModRecipeDefinition : IModContentDefinition
{
	/// <summary>The mod-scoped content id: a canonical lower-case path segment, unique within the registering mod.</summary>
	public string Id { get; set; } = "";

	/// <summary>The content kind this definition registers under - fixed by its type, never chosen by a caller.</summary>
	public string Kind => ModContentKind.Recipe;

	/// <summary>The mod-owned content schema version the framework stores verbatim (default 1).</summary>
	public int SchemaVersion { get; set; } = 1;

	/// <summary>The item/liquid id produced by the recipe.</summary>
	public string ResultItemId { get; set; } = "";

	/// <summary>True when the result is a liquid (not an item prefab).</summary>
	public bool ResultIsLiquid { get; set; }

	/// <summary>Result stack amount (defaults to 1).</summary>
	public int ResultAmount { get; set; } = 1;

	/// <summary>Result condition/amount fraction applied at spawn time.</summary>
	public float ResultCondition { get; set; } = 1f;

	/// <summary>True when the result should keep its container-liquid contents.</summary>
	public bool DontDrainResultLiquid { get; set; }

	/// <summary>The intelligence requirement for this recipe (0 makes it visible early).</summary>
	public int Intelligence { get; set; }

	/// <summary>The recipe category (see <see cref="ModRecipeCategory"/>).</summary>
	public string Category { get; set; } = ModRecipeCategory.Materials;

	/// <summary>True when this is a repair recipe (the result id is allowed as an ingredient).</summary>
	public bool IsRepair { get; set; }

	/// <summary>The ordered ingredient requirements.</summary>
	public List<ModRecipeIngredient> Ingredients
	{
		get;
		set => field = value ?? [];
	} = [];

}
