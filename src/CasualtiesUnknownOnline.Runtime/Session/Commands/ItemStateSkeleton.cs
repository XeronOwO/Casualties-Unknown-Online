using System.Text;
using CasualtiesUnknownOnline.Runtime.GameAdapter;

namespace CasualtiesUnknownOnline.Runtime.Session.Commands;

/// <summary>
/// The ONE place an item state's SHAPE becomes a document a player can start from. Completion offers it when the caret sits after a resolved item id and nothing has
/// been typed yet: Minecraft offers the component block there, and a console that offers nothing makes the state language undiscoverable — the user's 2026-10-30
/// report ("nothing completes after the id").
/// </summary>
/// <remarks>
/// The skeleton is deliberately PARSEABLE and minimal: a compound becomes <c>{Key: value, …}</c> with one default per key, a list becomes <c>[]</c>, and a scalar
/// becomes the value its kind admits (<c>0</c>, <c>""</c>, <c>false</c>, or its first named choice). The player edits values; they never have to guess the syntax of
/// a block they were never shown.
/// </remarks>
internal static class ItemStateSkeleton
{
	/// <summary>The document for a root shape, e.g. <c>{Condition: 1, Liquids: []}</c>.</summary>
	internal static string Of(ItemStateShape shape) => Value(shape);

	private static string Value(ItemStateShape shape) => shape switch
	{
		ItemStateCompoundShape compound => Compound(compound),
		ItemStateListShape => "[]",
		ItemStateScalarShape scalar => Scalar(scalar),
		_ => "{}",
	};

	private static string Compound(ItemStateCompoundShape compound)
	{
		if (compound.Keys.Count == 0)
		{
			return "{}";
		}

		var builder = new StringBuilder("{");
		for (var index = 0; index < compound.Keys.Count; index++)
		{
			if (index > 0)
			{
				builder.Append(", ");
			}

			var key = compound.Keys[index];
			builder.Append('\'').Append(key.Name).Append("': ").Append(Value(key.Shape));
		}

		return builder.Append('}').ToString();
	}

	private static string Scalar(ItemStateScalarShape scalar) => scalar.Kind switch
	{
		ItemStateValueKind.Number => "0",
		ItemStateValueKind.Bool => "false",
		ItemStateValueKind.String when scalar.Choices is { Count: > 0 } => "'" + scalar.Choices[0] + "'",
		_ => "''",
	};
}
