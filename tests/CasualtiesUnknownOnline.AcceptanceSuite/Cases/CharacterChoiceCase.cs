using System.Diagnostics;
using System.Globalization;
using System.Text.Json;

namespace CasualtiesUnknownOnline.AcceptanceSuite.Cases;

/// <summary>
/// A co-op session lets either client pick any of the three characters, and the session's ending
/// never writes the host's or the member's solo unlock keys. The native start screen derives its
/// locked portraits from <c>PlayerPrefs.GetInt("wonchar...")</c>; the session view repaints them and
/// the win patch skips <c>WorldGeneration.SetGameWon</c>. This case reads the three portraits after
/// creating the session, picks a locked character on each client, starts and joins, reads the body
/// each client actually spawned, then calls the host's Merc ending and checks both <c>wonchar</c>
/// keys are unchanged. The member joins through the normal handshake, so its choice travels through
/// the same static the game's own <c>StartRun</c> reads.
/// </summary>
internal sealed class CharacterChoiceCase : AcceptanceCase
{
	public override string Name => "character-choice";

	public override CaseCost Cost => CaseCost.NewWorld;

	public override async Task RunAsync(CaseContext context, CancellationToken cancellationToken)
	{
		var hostBefore = await context.RecipeAsync("character-read", cancellationToken).ConfigureAwait(false);
		var memberBefore = await context.MemberRecipeAsync("character-read", cancellationToken).ConfigureAwait(false);
		context.Read("host:before", hostBefore);
		context.Read("member:before", memberBefore);
		var hostChoice = FirstLocked(hostBefore);
		context.Note("host character choice: " + hostChoice);
		Require(hostChoice >= 0, "both of the host's solo unlock keys are already set; the missing-write guard cannot be observed: " + Raw(hostBefore));
		var memberChoice = hostChoice == 2 ? 1 : 2;

		try
		{
			context.Note(await context.Session.EnsureHostSessionAsync(cancellationToken).ConfigureAwait(false));
			var memberSessionTask = Task.Run(async () =>
			{
				await context.Session.ClearMemberSessionAsync(cancellationToken).ConfigureAwait(false);
				return await context.Session.EnsureMemberSessionAsync(cancellationToken).ConfigureAwait(false);
			}, cancellationToken);
			var picker = await WaitForPickerAsync(context, cancellationToken).ConfigureAwait(false);
			context.Read("picker", picker);
			var buttons = StringValue(picker, "buttons");
			var tooltips = StringValue(picker, "tooltips");
			Require(All(buttons, "true"), "the session did not make every character button pickable: buttons=" + buttons + " " + Raw(picker));
			Require(All(tooltips, "false"), "the session left a solo-lock tooltip on a character portrait: tooltips=" + tooltips + " " + Raw(picker));

			var hostPick = await context.Session.ActionAsync(
				context.Session.HostUrl,
				"pick-character",
				cancellationToken,
				("-Argument", hostChoice.ToString(CultureInfo.InvariantCulture))).ConfigureAwait(false);
			context.Read("host:pick", hostPick);
			Require(Int(hostPick, "selected") == hostChoice, "the host's picker did not take index " + hostChoice + ": " + Raw(hostPick));

			// The member may still be in the previous case's world; its picker only exists at the
			// menu, and a pick after the body has spawned is too late. The member session connects
			// while the host reads its picker and picks; then the member picks at the menu, and the
			// host starts the run.
			context.Note(await memberSessionTask.ConfigureAwait(false));
			var memberPick = await context.Session.ActionAsync(
				context.Session.MemberUrl,
				"pick-character",
				cancellationToken,
				("-Argument", memberChoice.ToString(CultureInfo.InvariantCulture))).ConfigureAwait(false);
			context.Read("member:pick", memberPick);
			Require(Int(memberPick, "selected") == memberChoice, "the member's picker did not take index " + memberChoice + ": " + Raw(memberPick));

			var preset = await context.RecipeAsync("run-preset", cancellationToken, ("name", "unchipped")).ConfigureAwait(false);
			context.Read("run preset", preset);
			Require(Boolean(preset, "ok") == true, "the host's unchipped preset could not be selected: " + Raw(preset));

			context.Note(await context.Session.StartHostRunAsync(cancellationToken).ConfigureAwait(false));
			await context.WaitForHostBodyAsync(cancellationToken).ConfigureAwait(false);
			var hostBody = await context.RecipeAsync("character-read", cancellationToken).ConfigureAwait(false);
			context.Read("host:body", hostBody);
			Require(Int(hostBody, "charType") == hostChoice, "the host spawned character " + Int(hostBody, "charType") + ", not " + hostChoice + ": " + Raw(hostBody));

			await context.Session.WaitForMemberInCurrentRunAsync(cancellationToken).ConfigureAwait(false);
			await context.WaitForMemberBodyAsync(cancellationToken).ConfigureAwait(false);
			var memberBody = await context.MemberRecipeAsync("character-read", cancellationToken).ConfigureAwait(false);
			context.Read("member:body", memberBody);
			Require(Int(memberBody, "charType") == memberChoice, "the member spawned character " + Int(memberBody, "charType") + ", not " + memberChoice + ": " + Raw(memberBody));

			var hostJoined = await WaitForRemoteCharTypeAsync(context, memberSide: false, memberChoice, cancellationToken).ConfigureAwait(false);
			var memberJoined = await WaitForRemoteCharTypeAsync(context, memberSide: true, hostChoice, cancellationToken).ConfigureAwait(false);
			context.Read("host:joined", hostJoined);
			context.Read("member:joined", memberJoined);
			// Row 2: the other client's display body carries the chosen character. `character-read`
			// lists the bodies with CUO's RemoteBodyDriver, which is exactly the remote display set.
			Require(HasCharType(hostJoined, memberChoice), "the host's remote display does not carry the member's character " + memberChoice + ": " + Raw(hostJoined));
			Require(HasCharType(memberJoined, hostChoice), "the member's remote display does not carry the host's character " + hostChoice + ": " + Raw(memberJoined));
			RequireSameUnlocks(hostBefore, hostJoined, "host after the member joined");
			RequireSameUnlocks(memberBefore, memberJoined, "member after joining");

			// The ending and the outside solo picker run after the shared unchipped world has served
			// the late-layer, pod and last-layer rows; CharacterSoloLockCase owns them.
		}
		finally
		{
			// The next run must not inherit unchipped; this world is reused by the late-layer rows.
			try
			{
				await context.RecipeAsync("run-preset", cancellationToken, ("name", "")).ConfigureAwait(false);
			}
			catch (Exception exception)
			{
				context.Note("could not clear the run preset after the character run: " + exception.Message);
			}

			// Leave the next new-world case on the default choice, without writing any unlock key.
			await TryPickAsync(context, 0, cancellationToken).ConfigureAwait(false);
			await TryPickMemberAsync(context, 0, cancellationToken).ConfigureAwait(false);
		}
	}

	/// <summary>
	/// The ending and the outside solo picker run after the shared unchipped world has served the
	/// late-layer, pod and last-layer rows. The ending ends that world, so this is the last row in it.
	/// </summary>
	internal static async Task RunSoloLockAsync(CaseContext context, CancellationToken cancellationToken)
	{
		var hostBefore = await context.RecipeAsync("character-read", cancellationToken).ConfigureAwait(false);
		var memberBefore = await context.MemberRecipeAsync("character-read", cancellationToken).ConfigureAwait(false);
		context.Read("host:before-ending", hostBefore);
		context.Read("member:before-ending", memberBefore);
		var hostChoice = FirstLocked(hostBefore);
		Require(hostChoice >= 0, "both of the host's solo unlock keys are already set; the missing-write guard cannot be observed: " + Raw(hostBefore));

		try
		{
			await TriggerSessionWinAsync(context, cancellationToken).ConfigureAwait(false);
			await Task.Delay(TimeSpan.FromMilliseconds(250), cancellationToken).ConfigureAwait(false);
			var hostAfterWin = await context.RecipeAsync("character-read", cancellationToken).ConfigureAwait(false);
			var memberAfterWin = await context.MemberRecipeAsync("character-read", cancellationToken).ConfigureAwait(false);
			context.Read("host:after-win", hostAfterWin);
			context.Read("member:after-win", memberAfterWin);
			RequireSameUnlocks(hostBefore, hostAfterWin, "host after the session's ending");
			RequireSameUnlocks(memberBefore, memberAfterWin, "member after the session's ending");

			// Leave the session and read the native solo picker again. The host still has the next
			// character locked, so its unlocked-only state must come back exactly.
			await LeaveHostSessionAsync(context, cancellationToken).ConfigureAwait(false);
			var outside = await WaitForPickerAsync(context, cancellationToken).ConfigureAwait(false);
			context.Read("picker:outside", outside);
			Require(Boolean(outside, "hasInstance") == true, "the host's pre-run picker did not load after leaving the session: " + Raw(outside));
			Require(ButtonAt(outside, "buttons", hostChoice) == "false", "the host's solo picker still offers the locked character after the session: " + Raw(outside));
			Require(ButtonAt(outside, "tooltips", hostChoice) == "true", "the host's solo picker lost the unlock tooltip after the session: " + Raw(outside));
			RequireSameUnlocks(hostBefore, outside, "host outside the session");
		}
		finally
		{
			await TryPickAsync(context, 0, cancellationToken).ConfigureAwait(false);
			await TryPickMemberAsync(context, 0, cancellationToken).ConfigureAwait(false);
		}
	}

	private static async Task<JsonElement?> WaitForPickerAsync(CaseContext context, CancellationToken cancellationToken)
	{
		JsonElement? picker = null;
		for (var attempt = 0; attempt < 30; attempt++)
		{
			picker = await context.RecipeAsync("character-picker", cancellationToken).ConfigureAwait(false);
			if (Boolean(picker, "hasInstance") == true
				&& StringValue(picker, "buttons").Length > 0
				&& StringValue(picker, "tooltips").Length > 0)
			{
				return picker;
			}

			await Task.Delay(TimeSpan.FromMilliseconds(250), cancellationToken).ConfigureAwait(false);
		}

		return picker;
	}

	private static async Task<JsonElement?> WaitForRemoteCharTypeAsync(CaseContext context, bool memberSide, int expected, CancellationToken cancellationToken)
	{
		// The remote clone is built after the peer's body report; the member's own body can exist a
		// moment before the host has drawn it. A single read at that instant measured an empty
		// remoteCharTypes in the full order (2026-11-15), while the focused run happened to be warm.
		JsonElement? reading = null;
		for (var attempt = 0; attempt < 40; attempt++)
		{
			reading = memberSide
				? await context.MemberRecipeAsync("character-read", cancellationToken).ConfigureAwait(false)
				: await context.RecipeAsync("character-read", cancellationToken).ConfigureAwait(false);
			if (HasCharType(reading, expected))
			{
				return reading;
			}

			await Task.Delay(TimeSpan.FromMilliseconds(250), cancellationToken).ConfigureAwait(false);
		}

		return reading;
	}

	private static bool HasCharType(JsonElement? reading, int expected)
	{
		var csv = StringValue(reading, "remoteCharTypes");
		foreach (var part in csv.Split(','))
		{
			if (int.TryParse(part, NumberStyles.Integer, CultureInfo.InvariantCulture, out var number) && number == expected)
			{
				return true;
			}
		}

		return false;
	}

	private static async Task TriggerSessionWinAsync(CaseContext context, CancellationToken cancellationToken)
	{
		await context.RecipeAsync("run-ending-a", cancellationToken, ("mode", "reset"), ("kind", "merc")).ConfigureAwait(false);
		var called = await context.RecipeAsync("run-ending-a", cancellationToken, ("mode", "call"), ("kind", "merc")).ConfigureAwait(false);
		context.Read("ending", called);
		var current = called;
		var deadline = Stopwatch.StartNew();
		while (deadline.Elapsed < TimeSpan.FromSeconds(5))
		{
			var text = current is { ValueKind: JsonValueKind.String } value ? value.GetString() ?? string.Empty : Raw(current);
			if (text.Contains("ending=Merc", StringComparison.Ordinal))
			{
				return;
			}

			await Task.Delay(TimeSpan.FromMilliseconds(250), cancellationToken).ConfigureAwait(false);
			current = await context.RecipeAsync("run-ending-a", cancellationToken, ("mode", "read"), ("kind", "merc")).ConfigureAwait(false);
		}

		context.Read("ending-final", current);
		var final = current is { ValueKind: JsonValueKind.String } last ? last.GetString() ?? string.Empty : Raw(current);
		Require(final.Contains("ending=Merc", StringComparison.Ordinal), "the host's Merc ending did not run: " + final);
	}

	private static async Task LeaveHostSessionAsync(CaseContext context, CancellationToken cancellationToken)
	{
		try
		{
			await context.Session.ActionAsync(context.Session.HostUrl, "leave-world", cancellationToken).ConfigureAwait(false);
			await context.Session.WaitWorldExitAsync(context.Session.HostUrl, cancellationToken).ConfigureAwait(false);
		}
		catch (Exception exception)
		{
			context.Note("the host was already out of the world: " + exception.Message);
		}

		try
		{
			await context.Session.ActionAsync(
				context.Session.HostUrl,
				"click",
				cancellationToken,
				("-Control", "home.leave")).ConfigureAwait(false);
			await Task.Delay(TimeSpan.FromMilliseconds(750), cancellationToken).ConfigureAwait(false);
		}
		catch (Exception exception)
		{
			context.Note("could not leave the host's session: " + exception.Message);
		}
	}

	private static string ButtonAt(JsonElement? reading, string property, int index)
	{
		var parts = StringValue(reading, property).Split(',');
		return index >= 0 && index < parts.Length ? parts[index] : string.Empty;
	}

	private static async Task TryPickAsync(CaseContext context, int choice, CancellationToken cancellationToken)
	{
		try
		{
			await context.Session.ActionAsync(
				context.Session.HostUrl,
				"pick-character",
				cancellationToken,
				("-Argument", choice.ToString(CultureInfo.InvariantCulture))).ConfigureAwait(false);
		}
		catch (Exception exception)
		{
			context.Note("could not restore the host's character choice: " + exception.Message);
		}
	}

	private static async Task TryPickMemberAsync(CaseContext context, int choice, CancellationToken cancellationToken)
	{
		try
		{
			await context.Session.ActionAsync(
				context.Session.MemberUrl,
				"pick-character",
				cancellationToken,
				("-Argument", choice.ToString(CultureInfo.InvariantCulture))).ConfigureAwait(false);
		}
		catch (Exception exception)
		{
			context.Note("could not restore the member's character choice: " + exception.Message);
		}
	}

	private static int FirstLocked(JsonElement? reading)
	{
		// `wonchar<character-1>` gates the next character: wonchar0 gates index 1, wonchar1
		// gates index 2. A zero key means that character is still locked.
		if (Int(reading, "wonchar0") == 0) { return 1; }
		if (Int(reading, "wonchar1") == 0) { return 2; }
		return -1;
	}

	private static void RequireSameUnlocks(JsonElement? before, JsonElement? after, string when)
	{
		Require(Int(after, "wonchar0") == Int(before, "wonchar0"),
			when + " changed wonchar0 from " + Int(before, "wonchar0") + " to " + Int(after, "wonchar0") + ": " + Raw(after));
		Require(Int(after, "wonchar1") == Int(before, "wonchar1"),
			when + " changed wonchar1 from " + Int(before, "wonchar1") + " to " + Int(after, "wonchar1") + ": " + Raw(after));
	}

	private static int Int(JsonElement? reading, string property) =>
		reading is { ValueKind: JsonValueKind.Object } element
			&& element.TryGetProperty(property, out var value)
			&& value.ValueKind == JsonValueKind.Number
			&& value.TryGetInt32(out var number)
				? number
				: -1;

	private static bool? Boolean(JsonElement? reading, string property) =>
		reading is { ValueKind: JsonValueKind.Object } element
			&& element.TryGetProperty(property, out var value)
				? value.ValueKind == JsonValueKind.True
				: null;

	private static bool All(string csv, string expected)
	{
		var parts = csv.Split(',');
		if (parts.Length != 3) { return false; }
		for (var i = 0; i < parts.Length; i++)
		{
			if (!string.Equals(parts[i], expected, StringComparison.Ordinal)) { return false; }
		}

		return true;
	}

	private static string StringValue(JsonElement? reading, string property) =>
		reading is { ValueKind: JsonValueKind.Object } element
			&& element.TryGetProperty(property, out var value)
			&& value.ValueKind == JsonValueKind.String
				? value.GetString() ?? string.Empty
				: string.Empty;

	private static string Raw(JsonElement? reading) => reading?.GetRawText() ?? "null";

	private static void Require(bool condition, string message)
	{
		if (!condition)
		{
			throw new AssertionException(message);
		}
	}
}
