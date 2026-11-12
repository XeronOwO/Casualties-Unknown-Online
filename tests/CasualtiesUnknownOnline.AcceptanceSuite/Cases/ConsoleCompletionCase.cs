using System.Text.Json;

namespace CasualtiesUnknownOnline.AcceptanceSuite.Cases;

/// <summary>
/// The console's command projection and its own line buffer come from the same service: the registry
/// answers the commands the player may run (by name) and the buffer keeps what was printed. The row
/// came from the console batch; the recipe reads both from the service the UI reads, never a copy.
/// </summary>
internal sealed class ConsoleCompletionCase : AcceptanceCase
{
	public override string Name => "console-completion";

	public override async Task RunAsync(CaseContext context, CancellationToken cancellationToken)
	{
		var registry = await context.RecipeAsync("console-registry", cancellationToken);
		context.Read("registry", registry);
		Require(Boolean(registry, "ok"), "the console registry did not answer: " + Raw(registry));
		Require(Count(registry, "commands") >= 1, "the console offered no commands: " + Raw(registry));
		var names = StringValue(registry, "names");
		Require(names.Contains("help", StringComparison.OrdinalIgnoreCase),
			"the registry does not name the help command: " + Raw(registry));
		Require(names.Contains("players", StringComparison.OrdinalIgnoreCase),
			"the registry does not name the players command: " + Raw(registry));

		var buffer = await context.RecipeAsync("console-read", cancellationToken, ("count", "2"));
		context.Read("buffer", buffer);
		Require(Boolean(buffer, "ok"), "the console buffer did not answer: " + Raw(buffer));
		Require(Count(buffer, "count") >= 1, "the console buffer is empty: " + Raw(buffer));
	}

	private static int Count(JsonElement? reading, string property) =>
		reading is { ValueKind: JsonValueKind.Object } element
		&& element.TryGetProperty(property, out var value)
		&& value.ValueKind == JsonValueKind.Number
			? value.GetInt32()
			: -1;

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
