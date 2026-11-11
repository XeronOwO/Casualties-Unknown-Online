using System.Text.Json;

namespace CasualtiesUnknownOnline.AcceptanceSuite.Cases;

/// <summary>
/// A prosthetic set on a limb is the game's own native `Limb.ProstheticInfo` and reads back on the
/// same body — the state the wire carries for a remote player's limb. The row came from the
/// prosthetics acceptance batch; the recipe performs exactly the call the item's use action makes.
/// </summary>
internal sealed class ProstheticCase : AcceptanceCase
{
	public override string Name => "prosthetic";

	public override async Task RunAsync(CaseContext context, CancellationToken cancellationToken)
	{
		var set = await context.RecipeAsync("limb-prosthetic4", cancellationToken,
			("mode", "set"),
			("limb", "3"),
			("item", "hook"));
		context.Read("set", set);
		Require(Boolean(set, "ok"), "the prosthetic was not set: " + Raw(set));
		var result = StringValue(set, "result");
		Require(result.Contains("set:True:hook", StringComparison.Ordinal), "the set answered another prosthetic: " + result);

		var read = await context.RecipeAsync("limb-prosthetic4", cancellationToken,
			("mode", "read"),
			("limb", "3"),
			("item", ""));
		context.Read("read", read);
		Require(Boolean(read, "ok"), "the prosthetic read failed: " + Raw(read));
		var parts = StringValue(read, "result");
		Require(parts.Contains("3=hook/", StringComparison.Ordinal), "the reading does not list the prosthetic: " + parts);
	}

	private static string Raw(JsonElement? reading) => reading?.GetRawText() ?? "null";

	private static string StringValue(JsonElement? reading, string property) =>
		reading is { ValueKind: JsonValueKind.Object } element
		&& element.TryGetProperty(property, out var value)
		&& value.ValueKind == JsonValueKind.String
			? value.GetString() ?? string.Empty
			: string.Empty;

	private static bool Boolean(JsonElement? reading, string property) =>
		reading is { ValueKind: JsonValueKind.Object } element
		&& element.TryGetProperty(property, out var value)
		&& value.ValueKind == JsonValueKind.True;

	private static void Require(bool condition, string message)
	{
		if (!condition)
		{
			throw new AssertionException(message);
		}
	}
}
