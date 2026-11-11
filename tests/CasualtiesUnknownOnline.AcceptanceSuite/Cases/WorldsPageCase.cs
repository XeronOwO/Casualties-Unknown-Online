namespace CasualtiesUnknownOnline.AcceptanceSuite.Cases;

/// <summary>
/// The Worlds page is the save surface: the window opens on it, the retention line states the
/// ceiling, and each run the archive knows is listed with its save points. The reading is the
/// page's own dump — the labels the player sees.
/// </summary>
internal sealed class WorldsPageCase : AcceptanceCase
{
	public override string Name => "worlds-page";

	public override async Task RunAsync(CaseContext context, CancellationToken cancellationToken)
	{
		await context.Session.ActionAsync(context.Session.HostUrl, "open-window", cancellationToken).ConfigureAwait(false);
		await Task.Delay(TimeSpan.FromMilliseconds(500), cancellationToken).ConfigureAwait(false);
		await context.Session.ActionAsync(context.Session.HostUrl, "click", cancellationToken, ("-Control", "tab.worlds")).ConfigureAwait(false);

		// The page is open when the window says so; the dump below is only the page the state names.
		var state = await context.Session
			.WaitUntilAsync(context.Session.HostUrl, candidate => Page(candidate).Contains("World", StringComparison.OrdinalIgnoreCase), TimeSpan.FromSeconds(5), cancellationToken)
			.ConfigureAwait(false);
		context.Read("state", state);
		var page = Page(state);
		Require(page.Contains("World", StringComparison.OrdinalIgnoreCase), "the worlds page did not open (page: '" + page + "')");

		var dump = await context.RecipeAsync("cuo-ui-dump", cancellationToken).ConfigureAwait(false);
		context.Read("dump", dump);
		var text = dump?.GetRawText() ?? "";
		Require(text.Contains("保留最新", StringComparison.Ordinal), "the retention line is missing from the worlds page");
		Require(text.Contains("个存档点", StringComparison.Ordinal), "no run row names its save points");

		// Close the window so the next case starts from the game, not the overlay.
		await context.RecipeAsync("virtual-key", cancellationToken, ("key", "Escape"), ("down", "1")).ConfigureAwait(false);
		await context.RecipeAsync("virtual-key", cancellationToken, ("key", "Escape"), ("down", "0")).ConfigureAwait(false);
		await Task.Delay(TimeSpan.FromMilliseconds(400), cancellationToken).ConfigureAwait(false);
	}

	private static string Page(System.Text.Json.JsonElement state) =>
		state is { ValueKind: System.Text.Json.JsonValueKind.Object } element
			&& element.TryGetProperty("page", out var page)
			&& page.ValueKind == System.Text.Json.JsonValueKind.String
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
