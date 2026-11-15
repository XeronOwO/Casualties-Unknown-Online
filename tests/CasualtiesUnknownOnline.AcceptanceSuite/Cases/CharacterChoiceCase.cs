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

			context.Note(await context.Session.StartHostRunAsync(cancellationToken).ConfigureAwait(false));
			await context.WaitForHostBodyAsync(cancellationToken).ConfigureAwait(false);
			var hostBody = await context.RecipeAsync("character-read", cancellationToken).ConfigureAwait(false);
			context.Read("host:body", hostBody);
			Require(Int(hostBody, "charType") == hostChoice, "the host spawned character " + Int(hostBody, "charType") + ", not " + hostChoice + ": " + Raw(hostBody));

			var memberPick = await context.Session.ActionAsync(
				context.Session.MemberUrl,
				"pick-character",
				cancellationToken,
				("-Argument", memberChoice.ToString(CultureInfo.InvariantCulture))).ConfigureAwait(false);
			context.Read("member:pick", memberPick);
			Require(Int(memberPick, "selected") == memberChoice, "the member's picker did not take index " + memberChoice + ": " + Raw(memberPick));

			context.Note(await context.Session.EnsureMemberAsync(cancellationToken).ConfigureAwait(false));
			await context.WaitForMemberBodyAsync(cancellationToken).ConfigureAwait(false);
			var memberBody = await context.MemberRecipeAsync("character-read", cancellationToken).ConfigureAwait(false);
			context.Read("member:body", memberBody);
			Require(Int(memberBody, "charType") == memberChoice, "the member spawned character " + Int(memberBody, "charType") + ", not " + memberChoice + ": " + Raw(memberBody));

			var hostJoined = await context.RecipeAsync("character-read", cancellationToken).ConfigureAwait(false);
			var memberJoined = await context.MemberRecipeAsync("character-read", cancellationToken).ConfigureAwait(false);
			context.Read("host:joined", hostJoined);
			context.Read("member:joined", memberJoined);
			RequireSameUnlocks(hostBefore, hostJoined, "host after the member joined");
			RequireSameUnlocks(memberBefore, memberJoined, "member after joining");

			await TriggerSessionWinAsync(context, cancellationToken).ConfigureAwait(false);
			await Task.Delay(TimeSpan.FromMilliseconds(500), cancellationToken).ConfigureAwait(false);
			var hostAfterWin = await context.RecipeAsync("character-read", cancellationToken).ConfigureAwait(false);
			var memberAfterWin = await context.MemberRecipeAsync("character-read", cancellationToken).ConfigureAwait(false);
			context.Read("host:after-win", hostAfterWin);
			context.Read("member:after-win", memberAfterWin);
			RequireSameUnlocks(hostBefore, hostAfterWin, "host after the session's ending");
			RequireSameUnlocks(memberBefore, memberAfterWin, "member after the session's ending");
		}
		finally
		{
			// Leave the next new-world case on the default choice, without writing any unlock key.
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

	private static async Task TriggerSessionWinAsync(CaseContext context, CancellationToken cancellationToken)
	{
		var declaration = Path.Combine(RepositoryPaths.Root, "tools", "acceptance", "driver", "eval-declarations", "run-ending-a.cs");
		var source = await File.ReadAllTextAsync(declaration, cancellationToken).ConfigureAwait(false);
		var declared = await context.Session.ActionAsync(
			context.Session.HostUrl,
			"declare",
			cancellationToken,
			("-Declare", "run-ending-a"),
			("-Text", source)).ConfigureAwait(false);
		context.Note("declaration: " + Raw(declared));
		var called = await context.RecipeAsync("run-ending-a", cancellationToken, ("mode", "call"), ("kind", "merc")).ConfigureAwait(false);
		context.Read("ending", called);
		var text = called is { ValueKind: JsonValueKind.String } value ? value.GetString() ?? string.Empty : Raw(called);
		Require(text.Contains("ending=Merc", StringComparison.Ordinal), "the host's Merc ending did not run: " + text);
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
		if (Int(reading, "wonchar0") == 0) { return 0; }
		if (Int(reading, "wonchar1") == 0) { return 1; }
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
