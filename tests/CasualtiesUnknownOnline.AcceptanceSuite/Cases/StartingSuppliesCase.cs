using System.Text.Json;

namespace CasualtiesUnknownOnline.AcceptanceSuite.Cases;

/// <summary>
/// A member that joins a run for the first time is a new player: the host's starting-supply
/// coordinator gives the new character the game's opening kit, and the member's own client
/// carries it. The case reads the member's carried items on the run it already joined — no
/// restart: a restart throws the session it is measuring away and pulls the member back to the
/// menu for no reason a player would recognise.
/// </summary>
internal sealed class StartingSuppliesCase : AcceptanceCase
{
	public override string Name => "starting-supplies";

	public override async Task RunAsync(CaseContext context, CancellationToken cancellationToken)
	{
		// The game's grant is decided by the run's own dropdown (`WorldGeneration.cs:2576-2589`, values
		// `none`/`light`/`full` = 0/1/2 from `RunSettings.cs:78`), so the row reads that number first and
		// then holds the member's inventory to exactly that plan — not to "some items with familiar names".
		var setting = await context.RecipeAsync("run-setting-read", cancellationToken, ("key", "startingsupplies")).ConfigureAwait(false);
		// A run that never carried the setting is `none` for both the game (`GetRunSettingInt` answers 0) and our
		// policy (`StartingSupplyPolicy`: anything outside 0/1/2 is NONE), so a missing key is level 0, not a failure.
		var value = string.Equals(Text(setting, "error"), "unknown-key", StringComparison.Ordinal) ? "0" : Text(setting, "value");
		Require(Boolean(setting, "ok") || string.Equals(Text(setting, "error"), "unknown-key", StringComparison.Ordinal),
			"the run's starting-supplies setting could not be read: " + Raw(setting));
		Require(int.TryParse(value, out var level) && level is >= 0 and <= 2,
			$"the run's starting-supplies setting is '{value}', which the game's own dropdown does not offer");

		string[] expected = level switch
		{
			0 => [],
			1 => ["emergencylight"],
			_ => ["lantern", "dogfood", "waterbottle", "trashbag"],
		};

		var actual = string.Empty;
		for (var wait = 0; wait < 40; wait++)
		{
			var items = await context.MemberRecipeAsync("container-read", cancellationToken, ("mode", "local"), ("guest", "")).ConfigureAwait(false);
			context.Read("member-items", items);
			actual = Ids(items);
			if (SameKit(actual, expected))
			{
				return;
			}

			await Task.Delay(TimeSpan.FromMilliseconds(250), cancellationToken).ConfigureAwait(false);
		}

		throw new AssertionException(
			$"the member carries [{actual}] on a '{value}' run ({SettingName(level)}), not the game's own plan [{string.Join(",", expected)}]");
	}

	private static string SettingName(int level) => level switch { 0 => "none", 1 => "light", _ => "full" };

	private static string[] Split(string ids) =>
		[.. ids.Split(',', StringSplitOptions.RemoveEmptyEntries).OrderBy(id => id, StringComparer.Ordinal)];

	private static bool SameKit(string actual, string[] expected) =>
		Split(actual).SequenceEqual(Split(string.Join(",", expected)), StringComparer.Ordinal);

	private static string Text(JsonElement? reading, string property) =>
		reading is { ValueKind: JsonValueKind.Object } element
		&& element.TryGetProperty(property, out var value)
		&& value.ValueKind == JsonValueKind.String
			? value.GetString() ?? string.Empty
			: string.Empty;

	private static bool Boolean(JsonElement? reading, string property) =>
		reading is { ValueKind: JsonValueKind.Object } element
		&& element.TryGetProperty(property, out var value)
		&& value.ValueKind == JsonValueKind.True;


	private static string Ids(JsonElement? reading)
	{
		if (reading is not { ValueKind: JsonValueKind.Object } element
			|| !element.TryGetProperty("items", out var items)
			|| items.ValueKind != JsonValueKind.Array)
		{
			return string.Empty;
		}

		var types = new List<string>();
		foreach (var item in items.EnumerateArray())
		{
			if (item.ValueKind == JsonValueKind.Object
				&& item.TryGetProperty("type", out var type)
				&& type.ValueKind == JsonValueKind.String)
			{
				types.Add(type.GetString() ?? string.Empty);
			}
		}

		return string.Join(",", types);
	}

	private static string Raw(JsonElement? reading) => reading?.GetRawText() ?? "null";

	private static void Require(bool condition, string message)
	{
		if (!condition)
		{
			throw new AssertionException(message);
		}
	}
}
