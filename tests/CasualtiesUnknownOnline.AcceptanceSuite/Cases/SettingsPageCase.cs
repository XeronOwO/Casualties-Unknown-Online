using System.Text.Json;

namespace CasualtiesUnknownOnline.AcceptanceSuite.Cases;

/// <summary>
/// The Preferences page is where the key bindings live: the window switches to it, the state names
/// the page, and the page's own dump carries the rows the player reads. The assertion is structural
/// (the page rendered its controls), not a copy of any label — the labels belong to the localization
/// files and a case that quoted them would fail on a wording change.
/// </summary>
internal sealed class SettingsPageCase : AcceptanceCase
{
	public override string Name => "settings-page";

	public override async Task RunAsync(CaseContext context, CancellationToken cancellationToken)
	{
		await context.Session.ActionAsync(context.Session.HostUrl, "open-window", cancellationToken).ConfigureAwait(false);
		await Task.Delay(TimeSpan.FromMilliseconds(500), cancellationToken).ConfigureAwait(false);
		await context.Session.ActionAsync(context.Session.HostUrl, "click", cancellationToken, ("-Control", "tab.preferences")).ConfigureAwait(false);

		var state = await context.Session
			.WaitUntilAsync(context.Session.HostUrl, candidate => Page(candidate).Contains("Preference", StringComparison.OrdinalIgnoreCase), TimeSpan.FromSeconds(5), cancellationToken)
			.ConfigureAwait(false);
		context.Read("state", state);
		Require(Page(state).Contains("Preference", StringComparison.OrdinalIgnoreCase), "the preferences page did not open (page: '" + Page(state) + "')");

		var dump = await context.RecipeAsync("cuo-ui-dump", cancellationToken).ConfigureAwait(false);
		context.Read("dump", dump);
		var text = dump?.GetRawText() ?? string.Empty;
		var rows = Count(text, "\"text\"");
		context.Note($"dump text nodes: {rows}");
		Require(rows >= 10, $"the preferences page drew only {rows} text nodes");

		// The page's own key binding read answers for the action rows the page draws.
		var bind = await context.RecipeAsync("keybind-read", cancellationToken, ("action", "interact"));
		context.Read("keybind", bind);
		Require(Boolean(bind, "ok"), "the key binding read failed: " + (bind?.GetRawText() ?? "null"));

		await context.RecipeAsync("virtual-key", cancellationToken, ("key", "Escape"), ("down", "1")).ConfigureAwait(false);
		await context.RecipeAsync("virtual-key", cancellationToken, ("key", "Escape"), ("down", "0")).ConfigureAwait(false);
		await Task.Delay(TimeSpan.FromMilliseconds(400), cancellationToken).ConfigureAwait(false);
	}

	private static int Count(string text, string needle)
	{
		var count = 0;
		var index = 0;
		while ((index = text.IndexOf(needle, index, StringComparison.Ordinal)) >= 0)
		{
			count++;
			index += needle.Length;
		}

		return count;
	}

	private static string Page(JsonElement state) =>
		state is { ValueKind: JsonValueKind.Object } element
			&& element.TryGetProperty("page", out var page)
			&& page.ValueKind == JsonValueKind.String
			? page.GetString() ?? ""
			: "";

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
