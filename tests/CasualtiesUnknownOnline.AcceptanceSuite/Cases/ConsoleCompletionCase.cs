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

		// The player's rule, as the game itself frames it: a console command acts on the local client only (design page
		// `docs/design/game/console-and-save.md`), and an unknown word is refused by the console rather than thrown. Our
		// console is its own catalogue, not the game's table — read both so the report shows the difference — and the row
		// judges what our console promises: its own answer for a word it knows, and a refusal (not a crash) for one it
		// does not.
		var game = await context.RecipeAsync("console-game", cancellationToken);
		context.Read("game", game);
		Require(Boolean(game, "ok"), "the game's own console table did not answer: " + Raw(game));
		Require(Count(game, "commands") > 0, "the game's own console table is empty: " + Raw(game));

		var beforeLines = Count(registry, "lines");
		// A console command is typed with the leading slash the console itself documents (`/help`); a bare word is chat,
		// which is why the row sends the line the player sends.
		var unknown = await context.RecipeAsync("console-run", cancellationToken, ("line", "/zzz-not-a-command"));
		context.Read("unknown", unknown);
		Require(Boolean(unknown, "ok"), "the console did not answer an unknown word: " + Raw(unknown));
		Require(StringValue(unknown, "error").Length == 0, "the console threw on an unknown word: " + Raw(unknown));
		Require(!Truthy(unknown, "accepted"), "the console ran a word no command owns: " + Raw(unknown));
		// The answer the player reads is the console's own line, not our recipe's opinion: the service writes
		// `console.command.unknown` when no command owns the first word (`CommandConsoleService.cs:232`).
		Require(Count(unknown, "lines") > beforeLines, "the console said nothing about an unknown word: " + Raw(unknown));
		Require(StringValue(unknown, "last").Contains("Unknown command", StringComparison.Ordinal),
			"the console did not answer the unknown word with its own refusal: " + Raw(unknown));

		var known = await context.RecipeAsync("console-run", cancellationToken, ("line", "/help"));
		context.Read("known", known);
		Require(Boolean(known, "ok"), "the console did not answer its own help: " + Raw(known));
		Require(Truthy(known, "accepted"), "the console refused its own help command: " + Raw(known));
		Require(Count(known, "lines") > Count(unknown, "lines"), "the help command wrote nothing into the console's buffer: " + Raw(known));
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

	private static bool Truthy(JsonElement? reading, string property) =>
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
