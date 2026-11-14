using System.Globalization;
using System.Text.Json;

namespace CasualtiesUnknownOnline.AcceptanceSuite.Cases;

/// <summary>
/// Watching a teammate is a presentation contract, not a camera move: the spectator's own client must draw the
/// WATCHED body's screen effects, limb/prosthetic strip and audible actions, while the spectator's own body stays
/// the control. This row proves the three families the local body used to leak through — the screen shader set
/// (PlayerCamera.HandleScreenShaders reads this.body throughout), the unconscious-screen set (black, ECG, sleep
/// fill, fade, vignette) and the WoundView limb/prosthetic strip (WoundView.cs:437-502) — plus the sound gate
/// (SoundPlayPatch lets the watched player's replays through and counts them).
/// </summary>
internal sealed class SpectatorPresentationCase : AcceptanceCase
{
	public override string Name => "spectator-presentation";

	public override async Task RunAsync(CaseContext context, CancellationToken cancellationToken)
	{
		await context.WaitForHostBodyAsync(cancellationToken).ConfigureAwait(false);
		await context.WaitForMemberBodyAsync(cancellationToken).ConfigureAwait(false);

		// Earlier rows can leave the native wound view open (medical-chart does). While it is open the bottom
		// strip belongs to that panel, not to the spectator; close it so this row measures the spectator HUD.
		var closedView = await context.RecipeAsync("wound-read", cancellationToken, ("toggle", "off")).ConfigureAwait(false);
		context.Note("closed any open wound view before spectating: " + Raw(closedView));
		await Task.Delay(TimeSpan.FromMilliseconds(400), cancellationToken).ConfigureAwait(false);

		var host = context.Session.HostUrl;
		var member = context.Session.MemberUrl;
		var memberId = context.MemberId;

		try
		{
			await ClearProstheticAsync(context, member, 3, cancellationToken).ConfigureAwait(false);
			await ClearProstheticAsync(context, host, 4, cancellationToken).ConfigureAwait(false);

			// The watched body: a prosthetic at limb 3 and pain at limb 5. The local host body carries a
			// prosthetic at limb 4 as the control: the strip must show the member's 3, not the host's 4.
			await MemberRecipeAsync(context, "limb-prosthetic4", cancellationToken, ("mode", "set"), ("limb", "3"), ("item", "steelprosthesis")).ConfigureAwait(false);
			await context.RecipeAsync("limb-prosthetic4", cancellationToken, ("mode", "set"), ("limb", "4"), ("item", "peo")).ConfigureAwait(false);

			await EnterSpectatorAsync(context, host, memberId, cancellationToken).ConfigureAwait(false);
			await context.Session.WaitForLogAsync(host, "[Spectator] mirror", 10_000, cancellationToken).ConfigureAwait(false);

			// Seed AFTER the watch starts: the host's simulated clone lets limb pain decay at the
			// game's own rate, so a wound seeded before the setup would be flat before the first read.
			await SeedWatchedPainAsync(context, cancellationToken).ConfigureAwait(false);

			// Pain and prosthetic: the screen shader and the limb strip are the watched body's.
			var presentation = await WaitForPresentationAsync(context, cancellationToken).ConfigureAwait(false);
			context.Read("spectator presentation", presentation);
			Require(Boolean(presentation, "ok"), "the spectator presentation probe did not answer ok: " + Raw(presentation));
			Require(!Boolean(presentation, "woundBodyIsCamera"), "the spectator's WoundView is still drawing the local body");
			Require(Boolean(presentation, "hudBodyIsWound"), "the spectator gate's HUD body is not the watched body");
			Require(Boolean(presentation, "woundLimb3Prosthetic"), "the watched body's prosthetic is not in the spectator's limb strip");
			Require(Text(presentation, "woundLimb3Item") == "steelprosthesis", "the strip shows the wrong prosthetic item: " + Text(presentation, "woundLimb3Item"));
			Require(Boolean(presentation, "cameraLimb4Prosthetic"), "the host's own prosthetic control is missing");
			Require(Number(presentation, "woundPain") >= 10, "the watched body carries no pain to mirror: " + Raw(presentation));
			Require(Number(presentation, "shaderPain") > 0.01, "the watched body's pain never reached the screen shader: " + Raw(presentation));

			// Sound: the watched member eats while the host is spectating; the remote replay must be counted by the
			// gate that silenced the local body's own audio family.
			var beforeSounds = Integer(presentation, "worldSoundsTotal");
			var provided = await context.MemberRecipeAsync("item-provide", cancellationToken, ("type", "dogfood"), ("mode", "create")).ConfigureAwait(false);
			Require(Boolean(provided, "ok"), "the member could not be given a snack: " + Raw(provided));
			await context.MemberRecipeAsync("item-use", cancellationToken, ("type", "dogfood"), ("aim-x", "0"), ("aim-y", "0")).ConfigureAwait(false);

			var heard = await WaitForSoundAsync(context, beforeSounds, cancellationToken).ConfigureAwait(false);
			context.Read("spectator sounds", heard);
			Require(Integer(heard, "worldSoundsTotal") > beforeSounds,
				"the watched player's action sound never reached the spectator: " + Raw(heard));

			// Unconscious-screen family: the watched body is forced out, and the whole screen presentation
			// (fade, sleep fill, ECG, vignette) must follow it instead of the local body's stale state.
			await MemberRecipeAsync(context, "body-force", cancellationToken,
				("sleep", "-1"), ("consciousness", "0"), ("energy", "0"), ("brainHealth", "1"),
				("badSleepAmount", "-1"), ("idleTime", "-1"), ("intelligence", "-1"),
				("heartRate", "-1"), ("bloodPressure", "-1"), ("bloodOxygen", "-1"), ("strength", "-1")).ConfigureAwait(false);

			var unconscious = await WaitForUnconsciousAsync(context, cancellationToken).ConfigureAwait(false);
			context.Read("spectator unconscious screen", unconscious);
			Require(Number(unconscious, "blackAmount") > 0.3, "the watched body's blackout did not reach the spectator: " + Raw(unconscious));
			Require(Number(unconscious, "consciousnessFadeAlpha") > 0.3, "the spectator's consciousness fade is not the watched body's: " + Raw(unconscious));
			Require(Number(unconscious, "vignetteAlpha") > 0.2, "the watched body's unconscious vignette did not reach the spectator: " + Raw(unconscious));
			Require(Boolean(unconscious, "ecgActive"), "the watched body's ECG did not activate on the spectator: " + Raw(unconscious));

			context.Note($"watched prosthetic {Text(presentation, "woundLimb3Item")}; pain {Number(presentation, "woundPain"):0.#} shader {Number(presentation, "shaderPain"):0.###}; sounds {Integer(heard, "worldSoundsTotal")}; blackout {Number(unconscious, "blackAmount"):0.##}");

			// Ending the watch destroys the followed clone. The HUD manager's private body field was pointed at
			// that clone; the next native MoodleManager.Update calls AddAllMoodles on it, so a failed restore is a
			// per-frame NullReferenceException storm (and the host UI stops responding). The local camera body must
			// be back in the field before the case is allowed to pass.
			await SafeConsoleAsync(context, host, "/spectate off", cancellationToken).ConfigureAwait(false);
			await SafeConsoleAsync(context, host, "/gamemode survivor", cancellationToken).ConfigureAwait(false);
			await Task.Delay(TimeSpan.FromMilliseconds(200), cancellationToken).ConfigureAwait(false);
			var released = await context.RecipeAsync("moodle-body", cancellationToken).ConfigureAwait(false);
			context.Read("spectator released HUD", released);
			Require(Boolean(released, "ok"), "the HUD body probe did not answer ok after the watch ended: " + Raw(released));
			Require(Boolean(released, "same"), "the HUD manager still holds the destroyed watched body after the watch ended: " + Raw(released));

			var stopped = await context.RecipeAsync("spectator-read", cancellationToken).ConfigureAwait(false);
			context.Read("spectator gate after release", stopped);
			Require(!Boolean(stopped, "gateActive"), "the spectator gate stayed active after the watch ended: " + Raw(stopped));
		}
		finally
		{
			await SafeConsoleAsync(context, host, "/spectate off", cancellationToken).ConfigureAwait(false);
			await SafeConsoleAsync(context, host, "/gamemode survivor", cancellationToken).ConfigureAwait(false);
			await SafeConsoleAsync(context, host, "/heal @a", cancellationToken).ConfigureAwait(false);
			await ClearProstheticAsync(context, host, 4, cancellationToken).ConfigureAwait(false);
			await ClearProstheticAsync(context, member, 3, cancellationToken).ConfigureAwait(false);
			await SafeMemberBodyRestoreAsync(context, cancellationToken).ConfigureAwait(false);
		}
	}

	private static async Task SeedWatchedPainAsync(CaseContext context, CancellationToken cancellationToken)
	{
		Exception? last = null;
		for (var attempt = 0; attempt < 4; attempt++)
		{
			try
			{
				var seeded = await context.MemberRecipeAsync("wound-seed", cancellationToken,
					("limb", "5"), ("force", "100"), ("infection", "0")).ConfigureAwait(false);
				context.Read("watched wound", seeded);
				Require(Boolean(seeded, "ok"), "the watched body's wound could not be seeded: " + Raw(seeded));

				// The clone recomputes averagePain when the limb report applies; give the 1 Hz/10 Hz snapshot a
				// moment before the spectator probe reads it.
				await Task.Delay(TimeSpan.FromMilliseconds(800), cancellationToken).ConfigureAwait(false);
				return;
			}
			catch (Exception exception) when (exception is DriverException or InvalidOperationException)
			{
				last = exception;
				await Task.Delay(TimeSpan.FromMilliseconds(250), cancellationToken).ConfigureAwait(false);
			}
		}

		throw new AssertionException("the watched body's wound could not be seeded after four attempts: " + last?.Message);
	}

	private static async Task EnterSpectatorAsync(CaseContext context, string host, string memberId, CancellationToken cancellationToken)
	{
		var mode = await context.Session.ActionAsync(host, "console", cancellationToken, ("-Text", "/gamemode spectator")).ConfigureAwait(false);
		context.Read("gamemode spectator", mode);
		Require(Boolean(mode, "applied"), "/gamemode spectator was refused: " + Raw(mode));
		var modeLine = Text(mode, "lastLine");
		Require(modeLine.Contains("Spectator mode", StringComparison.Ordinal), "/gamemode spectator did not report the mode: " + modeLine);

		// The console selector vocabulary is Minecraft's: a bare id matches nothing. The member's display
		// name may be absent, so name the SteamId the session always holds.
		var watch = await context.Session.ActionAsync(host, "console", cancellationToken, ("-Text", "/spectate @a[name=" + memberId + "]")).ConfigureAwait(false);
		context.Read("spectate", watch);
		Require(Boolean(watch, "applied"), "/spectate was refused: " + Raw(watch));
		var watchLine = Text(watch, "lastLine");
		Require(watchLine.Contains(memberId, StringComparison.Ordinal), "/spectate did not report the watched player: " + watchLine);
	}

	private static async Task<JsonElement?> WaitForPresentationAsync(CaseContext context, CancellationToken cancellationToken)
	{
		JsonElement? last = null;
		for (var attempt = 0; attempt < 40; attempt++)
		{
			last = await context.RecipeAsync("spectator-read", cancellationToken).ConfigureAwait(false);
			if (Boolean(last, "ok") && Integer(last, "worldSoundsTotal") >= 0 && Number(last, "woundPain") >= 10 && Number(last, "shaderPain") > 0.01)
			{
				return last;
			}

			await Task.Delay(TimeSpan.FromMilliseconds(250), cancellationToken).ConfigureAwait(false);
		}

		return last;
	}

	private static async Task<JsonElement?> WaitForSoundAsync(CaseContext context, int before, CancellationToken cancellationToken)
	{
		JsonElement? last = null;
		for (var attempt = 0; attempt < 40; attempt++)
		{
			last = await context.RecipeAsync("spectator-read", cancellationToken).ConfigureAwait(false);
			if (Integer(last, "worldSoundsTotal") > before)
			{
				return last;
			}

			await Task.Delay(TimeSpan.FromMilliseconds(250), cancellationToken).ConfigureAwait(false);
		}

		return last;
	}

	private static async Task<JsonElement?> WaitForUnconsciousAsync(CaseContext context, CancellationToken cancellationToken)
	{
		JsonElement? last = null;
		for (var attempt = 0; attempt < 40; attempt++)
		{
			last = await context.RecipeAsync("spectator-read", cancellationToken).ConfigureAwait(false);
			if (Number(last, "blackAmount") > 0.3 && Number(last, "consciousnessFadeAlpha") > 0.3 && Boolean(last, "ecgActive"))
			{
				return last;
			}

			await Task.Delay(TimeSpan.FromMilliseconds(250), cancellationToken).ConfigureAwait(false);
		}

		return last;
	}

	private static async Task ClearProstheticAsync(CaseContext context, string url, int limb, CancellationToken cancellationToken)
	{
		var args = new[] { ("mode", "clear"), ("limb", limb.ToString(CultureInfo.InvariantCulture)), ("item", "") };
		if (url == context.Session.HostUrl)
		{
			await context.RecipeAsync("limb-prosthetic4", cancellationToken, args).ConfigureAwait(false);
		}
		else
		{
			await context.MemberRecipeAsync("limb-prosthetic4", cancellationToken, args).ConfigureAwait(false);
		}
	}

	private static async Task SafeConsoleAsync(CaseContext context, string url, string text, CancellationToken cancellationToken)
	{
		try
		{
			await context.Session.ActionAsync(url, "console", cancellationToken, ("-Text", text)).ConfigureAwait(false);
		}
		catch
		{
			// Cleanup must not replace the assertion that failed.
		}
	}

	private static async Task SafeMemberBodyRestoreAsync(CaseContext context, CancellationToken cancellationToken)
	{
		try
		{
			await context.MemberRecipeAsync("body-force", cancellationToken,
				("sleep", "-1"), ("consciousness", "100"), ("energy", "100"), ("brainHealth", "100"),
				("badSleepAmount", "-1"), ("idleTime", "-1"), ("intelligence", "-1"),
				("heartRate", "-1"), ("bloodPressure", "-1"), ("bloodOxygen", "-1"), ("strength", "-1")).ConfigureAwait(false);
		}
		catch
		{
			// Cleanup only.
		}
	}

	private static async Task<JsonElement?> MemberRecipeAsync(CaseContext context, string name, CancellationToken cancellationToken, params (string Key, string Value)[] args) =>
		await context.MemberRecipeAsync(name, cancellationToken, args).ConfigureAwait(false);

	private static string Raw(JsonElement? reading) => reading?.GetRawText() ?? "null";

	private static string Text(JsonElement? reading, string property) =>
		reading is { ValueKind: JsonValueKind.Object } element
		&& element.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String
			? value.GetString() ?? string.Empty
			: string.Empty;

	private static double Number(JsonElement? reading, string property) =>
		reading is { ValueKind: JsonValueKind.Object } element
		&& element.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.Number
			? value.GetDouble()
			: -1;

	private static int Integer(JsonElement? reading, string property) =>
		reading is { ValueKind: JsonValueKind.Object } element
		&& element.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.Number
		&& value.TryGetInt32(out var number)
			? number
			: -1;

	private static bool Boolean(JsonElement? reading, string property) =>
		reading is { ValueKind: JsonValueKind.Object } element
		&& element.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.True;

	private static void Require(bool condition, string message)
	{
		if (!condition)
		{
			throw new AssertionException(message);
		}
	}
}
