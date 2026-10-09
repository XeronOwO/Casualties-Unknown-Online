using System.Collections.Generic;

namespace CasualtiesUnknownOnline.Abstractions;

/// <summary>
/// The framework's ready-made <see cref="IModRecipeDefinition"/>: a plain data
/// object in Abstractions with no game assembly, no Unity type and no Runtime
/// dependency, which is also why it cannot compute a member. Use it when every
/// value is a constant; implement the interface when one is computed.
///
/// It stays constructible with no arguments and every member stays settable on
/// purpose: the framework reads the interface, and a future data-driven loader
/// would deserialize into this concrete type.
/// </summary>
public sealed class ModRecipeDefinition : IModRecipeDefinition
{
	/// <inheritdoc />
	public string Id { get; set; } = "";

	/// <inheritdoc />
	public string Kind => ModContentKind.Recipe;

	/// <inheritdoc />
	public int SchemaVersion { get; set; } = 1;

	/// <inheritdoc />
	public string ResultItemId { get; set; } = "";

	/// <inheritdoc />
	public bool ResultIsLiquid { get; set; }

	/// <inheritdoc />
	public int ResultAmount { get; set; } = 1;

	/// <inheritdoc />
	public float ResultCondition { get; set; } = 1f;

	/// <inheritdoc />
	public bool DontDrainResultLiquid { get; set; }

	/// <inheritdoc />
	public int Intelligence { get; set; }

	/// <inheritdoc />
	public string Category { get; set; } = ModRecipeCategory.Materials;

	/// <inheritdoc />
	public bool IsRepair { get; set; }

	/// <inheritdoc />
	public List<ModRecipeIngredient> Ingredients
	{
		get;
		set => field = value ?? [];
	} = [];
}
