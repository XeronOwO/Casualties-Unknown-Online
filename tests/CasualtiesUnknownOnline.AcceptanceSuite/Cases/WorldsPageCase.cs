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
		await context.Session.ActionAsync(context.Session.HostUrl, "goto-page", cancellationToken, ("-Page", "worlds")).ConfigureAwait(false);
		await Task.Delay(TimeSpan.FromMilliseconds(800), cancellationToken).ConfigureAwait(false);

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

	private static void Require(bool condition, string message)
	{
		if (!condition)
		{
			throw new AssertionException(message);
		}
	}
}
