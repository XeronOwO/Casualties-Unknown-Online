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
		// The kit is granted by the host's coordinator once the new character stands in the world;
		// the member's own carried items are the marker. The count alone is not the row — the game's
		// own decay can take a unit before the read — so the case asks for the kit's fixed contents.
		var ids = string.Empty;
		var carried = 0;
		for (var wait = 0; wait < 40; wait++)
		{
			var items = await context.MemberRecipeAsync("container-read", cancellationToken, ("mode", "local"), ("guest", "")).ConfigureAwait(false);
			context.Read("member-items", items);
			carried = CountCarried(items);
			ids = Ids(items);
			if (carried >= 3 && IsStartingKit(ids))
			{
				break;
			}

			await Task.Delay(TimeSpan.FromMilliseconds(250), cancellationToken).ConfigureAwait(false);
		}

		Require(
			carried >= 3 && IsStartingKit(ids),
			$"the member carries {carried} item(s) [{ids}], not the game's starting kit");
	}

	/// <summary>The game's own starting-supply table (`StartingSupplyPolicy`, setting 1 "light").</summary>
	private static readonly string[] StartingKit = ["emergencylight", "lantern", "dogfood", "waterbottle", "trashbag"];

	private static bool IsStartingKit(string ids) =>
		ids.Length > 0 && ids.Split(',', StringSplitOptions.RemoveEmptyEntries).All(StartingKit.Contains);

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

	private static int CountCarried(JsonElement? reading)
	{
		if (reading is not { ValueKind: JsonValueKind.Object } element)
		{
			return 0;
		}

		foreach (var property in new[] { "localCount", "count" })
		{
			if (element.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.Number)
			{
				return value.GetInt32();
			}
		}

		return 0;
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
