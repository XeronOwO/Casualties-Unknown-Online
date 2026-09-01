namespace CasualtiesUnknownOnline.Abstractions;

/// <summary>
/// Stable recipe category names for <see cref="ModRecipeDefinition.Category"/>.
/// The values are plain strings so Abstractions stays free of game types; the
/// Game Adapter maps them to the vanilla <c>Recipes.RecipeCategory</c> enum.
/// </summary>
public static class ModRecipeCategory
{
	public const string Materials = "materials";
	public const string Tools = "tools";
	public const string Medicine = "medicine";
	public const string Utilities = "utilities";
	public const string Food = "food";
}
