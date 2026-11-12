using System.Text.Json;

namespace CasualtiesUnknownOnline.AcceptanceSuite.Cases;

/// <summary>
/// The world the host's own client generated: its own scene's building objects, counted by the prefab
/// id each entity carries. A world that generates nothing is a run nobody can play, and the count is
/// the reading the world domain of the sweep asks for. Each client generates its own layer by design,
/// so this row is the host's own world, never a cross-client comparison.
/// </summary>
internal sealed class WorldCensusCase : AcceptanceCase
{
	public override string Name => "world-census";

	public override async Task RunAsync(CaseContext context, CancellationToken cancellationToken)
	{
		await context.WaitForHostBodyAsync(cancellationToken).ConfigureAwait(false);

		var census = await context.RecipeAsync("world-object-census", cancellationToken).ConfigureAwait(false);
		context.Read("census", census);
		Require(Boolean(census, "ok") == true, "the world census could not be read: " + Raw(census));
		var total = Integer(census, "total");
		Require(total >= 1, $"the host's own world holds no building object (total {total}): " + Raw(census));
	}

	private static string Raw(JsonElement? reading) => reading?.GetRawText() ?? "null";

	private static bool? Boolean(JsonElement? reading, string property) =>
		reading is { ValueKind: JsonValueKind.Object } element
		&& element.TryGetProperty(property, out var value)
			? value.ValueKind == JsonValueKind.True
			: null;

	private static long Integer(JsonElement? reading, string property)
	{
		if (reading is not { ValueKind: JsonValueKind.Object } element || !element.TryGetProperty(property, out var value))
		{
			return -1;
		}

		return value.ValueKind switch
		{
			JsonValueKind.Number => value.GetInt64(),
			JsonValueKind.String when long.TryParse(value.GetString(), out var parsed) => parsed,
			_ => -1,
		};
	}

	private static void Require(bool condition, string message)
	{
		if (!condition)
		{
			throw new AssertionException(message);
		}
	}
}
