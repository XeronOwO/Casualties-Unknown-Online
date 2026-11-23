using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
namespace CasualtiesUnknownOnline.AcceptanceSuite.Cases;

/// <summary>
/// The Worlds page is the save surface: the window opens on it, the retention line states the
/// ceiling, and each run the archive knows is listed with its save points.
///
/// The player's path is the reading: ask the game for a save, wait for the cut to land, open the
/// page and read the rows it draws. A page that greets the eye with the right words but lists no
/// run is not the page the claim is about, so the row is red until a save point it just made is
/// named there (2026-11-24: the older row matched the label text alone, and a batch with an empty
/// archive passed it while the player saw no run at all).
/// </summary>
internal sealed class WorldsPageCase : AcceptanceCase
{
	private static readonly Regex Retention = new("保留最新\\s*(\\d+)\\s*份备份", RegexOptions.CultureInvariant);

	private static readonly Regex SavePoints = new("(\\d+)\\s*个存档点", RegexOptions.CultureInvariant);

	public override string Name => "worlds-page";

	public override async Task RunAsync(CaseContext context, CancellationToken cancellationToken)
	{
		// The archive only lists what the game has cut, so the row takes its own cut first.
		var save = await context.Session
			.ActionAsync(context.Session.HostUrl, "console", cancellationToken, ("-Text", "/save"))
			.ConfigureAwait(false);
		context.Read("save", save);

		await context.Session
			.ActionAsync(context.Session.HostUrl, "open-window", cancellationToken)
			.ConfigureAwait(false);
		await Task.Delay(TimeSpan.FromMilliseconds(100), cancellationToken).ConfigureAwait(false);
		await context.Session
			.ActionAsync(context.Session.HostUrl, "click", cancellationToken, ("-Control", "tab.worlds"))
			.ConfigureAwait(false);

		// The page is open when the window says so; the dump below is only the page the state names.
		var state = await context.Session
			.WaitUntilAsync(context.Session.HostUrl, candidate => Page(candidate).Contains("World", StringComparison.OrdinalIgnoreCase), TimeSpan.FromSeconds(5), cancellationToken)
			.ConfigureAwait(false);
		context.Read("state", state);
		var page = Page(state);
		Require(page.Contains("World", StringComparison.OrdinalIgnoreCase), "the worlds page did not open (page: '" + page + "')");

		// A save can take a few seconds to land; the page is read until it names the point just made.
		var text = "";
		var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(20);
		var points = 0;
		while (DateTime.UtcNow < deadline)
		{
			var dump = await context.RecipeAsync("cuo-ui-dump", cancellationToken).ConfigureAwait(false);
			text = dump?.GetRawText() ?? "";
			var match = SavePoints.Match(text);
			if (match.Success && int.TryParse(match.Groups[1].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var count) && count >= 1)
			{
				points = count;
				break;
			}

			await Task.Delay(TimeSpan.FromMilliseconds(500), cancellationToken).ConfigureAwait(false);
		}

		Require(points >= 1, "the worlds page names no save point after the run took one: " + Tail(text));

		// The retention line the player reads must also carry a ceiling, not only the phrase.
		var retention = Retention.Match(text);
		Require(retention.Success, "the retention line is missing its ceiling: " + Tail(text));
		Require(
			int.TryParse(retention.Groups[1].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var kept) && kept >= 1,
			"the retention line names no positive number of backups: " + retention.Value);

		context.Note("the page names " + points.ToString(CultureInfo.InvariantCulture) + " save point(s) and keeps " + retention.Groups[1].Value + " backup(s) per world");

		// Close the window so the next case starts from the game, not the overlay.
		await context.RecipeAsync("virtual-key", cancellationToken, ("key", "Escape"), ("down", "1")).ConfigureAwait(false);
		await context.RecipeAsync("virtual-key", cancellationToken, ("key", "Escape"), ("down", "0")).ConfigureAwait(false);
		await Task.Delay(TimeSpan.FromMilliseconds(400), cancellationToken).ConfigureAwait(false);
	}

	private static string Tail(string text) =>
		text.Length <= 400 ? text : "…" + text.Substring(text.Length - 400);

	private static string Page(JsonElement state) =>
		state is { ValueKind: JsonValueKind.Object } element
			&& element.TryGetProperty("page", out var page)
			&& page.ValueKind == JsonValueKind.String
			? page.GetString() ?? ""
			: "";

	private static void Require(bool condition, string message)
	{
		if (!condition)
		{
			throw new AssertionException(message);
		}
	}
}
