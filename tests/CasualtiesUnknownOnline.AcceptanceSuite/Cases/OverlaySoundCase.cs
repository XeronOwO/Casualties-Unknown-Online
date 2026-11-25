using System.Globalization;
using System.Text.Json;

namespace CasualtiesUnknownOnline.AcceptanceSuite.Cases;

/// <summary>
/// The player's rule: my menu is mine — clicking it is heard on my machine and never on my friend's, while the world's
/// own sounds still reach them. The game plays a UI click as a flat 2D sound on the machine that clicked
/// (`PlayerCamera` calls the game's own `Sound.Play`, which sets `spatialBlend = 0` for that clip), so a click arriving
/// on the other client can only be our product forwarding it. The row reads each machine's own playing clips: the
/// clicking side must hear its click (otherwise the action never happened), the other side must hear none, and a block
/// hit afterwards proves the other side's recorder is live and the world's sounds do arrive.
/// </summary>
internal sealed class OverlaySoundCase : AcceptanceCase
{
	public override string Name => "overlay-sound";

	public override async Task RunAsync(CaseContext context, CancellationToken cancellationToken)
	{
		await context.Session.ActionAsync(context.Session.HostUrl, "close-window", cancellationToken).ConfigureAwait(false);

		// Both recorders are armed before the click: the clicking side proves the click happened, the far side proves it
		// did not arrive. The recorder hooks the game's sound dispatch after the scene is ready, so arming is retried.
		Require(await ArmAsync(context, false, "click", cancellationToken).ConfigureAwait(false), "the host's sound recorder did not arm for clicks");
		Require(await ArmAsync(context, true, "click", cancellationToken).ConfigureAwait(false), "the member's sound recorder did not arm for clicks");

		await context.Session.ActionAsync(context.Session.HostUrl, "open-window", cancellationToken).ConfigureAwait(false);
		var clicked = await context.RecipeAsync("ui-intent-probe-a", cancellationToken, ("control", "home.refresh")).ConfigureAwait(false);
		context.Read("click", clicked);
		Require(Boolean(clicked, "ok"), "the host's own menu did not take the click: " + (clicked?.GetRawText() ?? "null"));

		await Task.Delay(TimeSpan.FromMilliseconds(400), cancellationToken).ConfigureAwait(false);
		var hostHeard = await ReadAsync(context, false, "click", cancellationToken).ConfigureAwait(false);
		var memberHeard = await ReadAsync(context, true, "click", cancellationToken).ConfigureAwait(false);
		context.Read("host-click", LineOf(hostHeard));
		context.Read("member-click", LineOf(memberHeard));
		Require(hostHeard.Contains("click@", StringComparison.Ordinal), $"the host's own click was not heard on its own machine: {hostHeard}");
		Require(!memberHeard.Contains("click@", StringComparison.Ordinal), $"the member heard the host's menu click, so our product forwards a menu sound: {memberHeard}");

		// A destroyed block is the control: the game plays its hit and step sounds only when the block is destroyed
		// (`WorldGeneration.DamageBlock` plays them inside the `damage >= health` branch), and every client plays its own
		// copy of that world sound. Without the control a silent member machine would pass the click check for the wrong
		// reason. The recorder's `raw=` counter counts every `Sound.Play` it saw, so the check is a count that grows.
		await ArmAsync(context, true, "hit", cancellationToken).ConfigureAwait(false);
		var scan = await context.RecipeAsync("block-find-breakable", cancellationToken, ("count", "1"), ("minOffset", "1"), ("radius", "8")).ConfigureAwait(false);
		if (FirstCell(scan) is null)
		{
			// The batch can leave the body at a spot with nothing breakable around it (measured 2026-11-25:
			// count 0 within 8 units). The control is the block, not the radius, so look further before failing.
			scan = await context.RecipeAsync("block-find-breakable", cancellationToken, ("count", "1"), ("minOffset", "1"), ("radius", "24")).ConfigureAwait(false);
		}

		context.Read("cell", scan);
		var target = FirstCell(scan);
		Require(target is not null, "the world offers no breakable block to hit: " + (scan?.GetRawText() ?? "null"));
		var before = RawCount(await ReadAsync(context, true, "hit", cancellationToken).ConfigureAwait(false));
		await context.RecipeAsync(
			"block-break-at",
			cancellationToken,
			("x", target!.Value.X.ToString(CultureInfo.InvariantCulture)),
			("y", target.Value.Y.ToString(CultureInfo.InvariantCulture)),
			("dmg", "1000")).ConfigureAwait(false);
		await Task.Delay(TimeSpan.FromMilliseconds(700), cancellationToken).ConfigureAwait(false);
		var after = RawCount(await ReadAsync(context, true, "hit", cancellationToken).ConfigureAwait(false));
		context.Read("member-hit-count", LineOf($"before={before} after={after}"));
		Require(after > before + 1, $"the member heard no world sound after a block was destroyed (sound calls {before} then {after}), so its recorder or the world's sound path is not live");
	}

	private static async Task<bool> ArmAsync(CaseContext context, bool member, string match, CancellationToken cancellationToken)
	{
		for (var attempt = 0; attempt < 6; attempt++)
		{
			var armed = await SoundAsync(context, member, "start", match, cancellationToken).ConfigureAwait(false);
			if (Boolean(armed, "ok"))
			{
				return true;
			}

			await Task.Delay(TimeSpan.FromMilliseconds(250), cancellationToken).ConfigureAwait(false);
		}

		return false;
	}

	private static async Task<string> ReadAsync(CaseContext context, bool member, string match, CancellationToken cancellationToken)
	{
		var read = await SoundAsync(context, member, "read", match, cancellationToken).ConfigureAwait(false);
		return read is { ValueKind: JsonValueKind.Object } element && element.TryGetProperty("line", out var value) && value.ValueKind == JsonValueKind.String
			? value.GetString() ?? ""
			: "";
	}

	private static Task<JsonElement?> SoundAsync(CaseContext context, bool member, string mode, string match, CancellationToken cancellationToken) =>
		member
			? context.MemberRecipeAsync("sound-log", cancellationToken, ("mode", mode), ("match", match))
			: context.RecipeAsync("sound-log", cancellationToken, ("mode", mode), ("match", match));

	// `context.Read` keeps JSON, so a long recorder line is trimmed to something the report shows in one line.
	private static JsonElement? LineOf(string line)
	{
		var trimmed = line.Length > 400 ? line[..400] : line;
		using var document = JsonDocument.Parse(JsonSerializer.Serialize(trimmed));
		return document.RootElement.Clone();
	}

	// `raw=<calls seen>|clip@time;…` — the leading count is every `Sound.Play` the recorder has seen.
	private static int RawCount(string line)
	{
		const string prefix = "raw=";
		if (!line.StartsWith(prefix, StringComparison.Ordinal))
		{
			return -1;
		}

		var end = line.IndexOf('|', prefix.Length);
		var digits = end < 0 ? line[prefix.Length..] : line[prefix.Length..end];
		return int.TryParse(digits, NumberStyles.Integer, CultureInfo.InvariantCulture, out var count) ? count : -1;
	}

	private static (int X, int Y)? FirstCell(JsonElement? reading) =>
		reading is { ValueKind: JsonValueKind.Object } element
		&& element.TryGetProperty("cells", out var cells)
		&& cells.ValueKind == JsonValueKind.Array
		&& cells.GetArrayLength() > 0
		&& cells[0] is { ValueKind: JsonValueKind.Object } cell
		&& cell.TryGetProperty("x", out var x)
		&& cell.TryGetProperty("y", out var y)
			? (x.GetInt32(), y.GetInt32())
			: null;

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
