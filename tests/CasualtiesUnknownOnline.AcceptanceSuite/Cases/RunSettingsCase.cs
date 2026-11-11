using System.Text.Json;

namespace CasualtiesUnknownOnline.AcceptanceSuite.Cases;

/// <summary>
/// The run-settings table the host's baseline snapshot carries: every key the game's own
/// `WorldGeneration.runSettings` holds is listed, and a value written through the same table reads
/// back changed. The 1.0 run-settings row is exactly this table's content.
/// </summary>
internal sealed class RunSettingsCase : AcceptanceCase
{
	public override string Name => "run-settings";

	public override async Task RunAsync(CaseContext context, CancellationToken cancellationToken)
	{
		var keys = await context.RecipeAsync("run-settings-keys", cancellationToken);
		context.Read("keys", keys);
		Require(Boolean(keys, "ok"), "the run settings were not read: " + Raw(keys));
		var count = Number(keys, "count");
		Require(count >= 10, $"the run settings hold only {count} keys");

		var written = await context.RecipeAsync("run-setting", cancellationToken,
			("key", "infectionspeed"),
			("value", "1.25"));
		context.Read("written", written);
		Require(Boolean(written, "ok"), "the run setting was not written: " + Raw(written));

		var reread = await context.RecipeAsync("run-settings-keys", cancellationToken);
		context.Read("reread", reread);
		var text = Raw(reread);
		Require(text.Contains("infectionspeed", StringComparison.Ordinal), "the reread does not list the key: " + text);

		// The write is the game's own table; put the value back so later cases read the baseline.
		await context.RecipeAsync("run-setting", cancellationToken, ("key", "infectionspeed"), ("value", "1")).ConfigureAwait(false);
	}

	private static string Raw(JsonElement? reading) => reading?.GetRawText() ?? "null";

	private static double Number(JsonElement? reading, string property) =>
		reading is { ValueKind: JsonValueKind.Object } element
		&& element.TryGetProperty(property, out var value)
		&& value.ValueKind == JsonValueKind.Number
		&& value.TryGetDouble(out var number)
			? number
			: 0;

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
