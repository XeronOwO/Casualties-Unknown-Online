using System.Text.Json;

namespace CasualtiesUnknownOnline.AcceptanceSuite.Cases;

/// <summary>
/// The overlay's controls play the game's own UI click: an intent reported through the surface's
/// own door is heard by the sound recorder the game's `Sound.Play` feeds.
/// </summary>
internal sealed class OverlaySoundCase : AcceptanceCase
{
	public override string Name => "overlay-sound";

	public override async Task RunAsync(CaseContext context, CancellationToken cancellationToken)
	{
		// The overlay's own controls answer only while its window is up, and the recorder hooks the
		// game's Sound after the scene is ready: the arming is retried instead of read once on the
		// frame the previous case left (measured: one run read "the sound recorder did not arm"
		// right after a restart, and the next run passed).
		await context.Session.ActionAsync(context.Session.HostUrl, "open-window", cancellationToken).ConfigureAwait(false);
		JsonElement? armed = null;
		for (var attempt = 0; attempt < 6; attempt++)
		{
			armed = await context.RecipeAsync("sound-log", cancellationToken, ("mode", "start"), ("match", "click")).ConfigureAwait(false);
			if (armed is { ValueKind: JsonValueKind.Object } armAnswer && armAnswer.TryGetProperty("ok", out var ok) && ok.ValueKind == JsonValueKind.True)
			{
				break;
			}

			await Task.Delay(TimeSpan.FromMilliseconds(500), cancellationToken).ConfigureAwait(false);
		}

		context.Read("armed", armed);
		Require(Boolean(armed, "ok"), "the sound recorder did not arm: " + (armed?.GetRawText() ?? "null"));

		var intent = await context.RecipeAsync("ui-intent-probe-a", cancellationToken, ("control", "home.refresh")).ConfigureAwait(false);
		context.Read("intent", intent);
		Require(Boolean(intent, "ok"), "the surface did not take the intent");

		await Task.Delay(TimeSpan.FromMilliseconds(400), cancellationToken).ConfigureAwait(false);
		var read = await context.RecipeAsync("sound-log", cancellationToken, ("mode", "read"), ("match", "click")).ConfigureAwait(false);
		context.Read("read", read);
		var line = read is { ValueKind: JsonValueKind.Object } element && element.TryGetProperty("line", out var value) && value.ValueKind == JsonValueKind.String
			? value.GetString() ?? ""
			: "";
		Require(line.Contains("click@", StringComparison.Ordinal), $"the click was not recorded: {line}");
	}

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
