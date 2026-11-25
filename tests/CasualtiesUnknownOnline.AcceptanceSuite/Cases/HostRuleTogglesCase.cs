using System.Globalization;
using System.Text.Json;

namespace CasualtiesUnknownOnline.AcceptanceSuite.Cases;

/// <summary>
/// The host-rule switches for the body-action families (the user's 2026-11-14 decision): a push refused on
/// the pusher's own client with a named reason while the target stays put, a carry the game's own rule would
/// refuse that the host's <c>allow_always_carry</c> lets through, and the fact that both values are ANNOUNCED —
/// the member's client reads the host's value, not its own config.
///
/// <para>
/// The gestures go through the same runtime entry the Online UI uses (<c>push-player</c> and <c>carry-start</c>
/// call <c>IPlayerInteractionControl</c>), the rule is written with the real console verb, and the member's
/// side is read with <c>host-rules-read</c>. It writes host rules and would leave the next row reading
/// state it changed, so it is registered after the rows that read the default rules, and it restores them
/// before it ends.
/// </para>
/// </summary>
internal sealed class HostRuleTogglesCase : AcceptanceCase
{
	public override string Name => "host-rule-toggles";

	/// <summary>
	/// The two switches the row writes. Its own restore runs on the row's normal path only, so a row that throws
	/// half-way leaves a switch the next row would run inside.
	/// </summary>
	public override CaseLeftovers Leaves => CaseLeftovers.Rules | CaseLeftovers.Position;

	public override CaseCost Cost => CaseCost.InRun;

	/// <summary>Writes host rules, so the registry places it after every case that reads the default rules.</summary>

	public override async Task RunAsync(CaseContext context, CancellationToken cancellationToken)
	{
		await context.Session.EnsureMemberAsync(cancellationToken).ConfigureAwait(false);
		var memberId = await SingleMemberAsync(context, cancellationToken).ConfigureAwait(false);
		context.UseMember(memberId);

		// The row starts from the shipped defaults, written through the same verb rather than assumed: a previous
		// run's console writes are persisted in the host's config, so the second run began with allow_push=false
		// (2026-11-18). The defaults a member acts on are the host's values, read back on the member.
		await SetHostRuleAsync(context, "allow_push", "true", cancellationToken).ConfigureAwait(false);
		await SetHostRuleAsync(context, "allow_always_carry", "false", cancellationToken).ConfigureAwait(false);
		await WaitForMemberRuleAsync(context, "allowPush", expected: true, cancellationToken).ConfigureAwait(false);
		var before = await MemberRulesAsync(context, cancellationToken).ConfigureAwait(false);
		Require(Boolean(before, "announced"), "the member had not received a rule announcement before the row started: " + Raw(before));
		Require(Boolean(before, "allowPush"), "the default allow_push was not true on the member: " + Raw(before));
		Require(!Boolean(before, "allowAlwaysCarry"), "the default allow_always_carry was not false on the member: " + Raw(before));

		// Row 1: with pushes off, the gesture is refused where it is made and the target does not move.
		await SetHostRuleAsync(context, "allow_push", "false", cancellationToken).ConfigureAwait(false);
		var pushed = await WaitForMemberRuleAsync(context, "allowPush", expected: false, cancellationToken).ConfigureAwait(false);
		Require(!Boolean(pushed, "allowPush"), "the member did not read the host's allow_push=false: " + Raw(pushed));

		// The host's own gate has to see the same write: it is the one that refuses the gesture, and a
		// console write that only reaches the announcement would leave the host reading its old value.
		var hostRules = await context.RecipeAsync("host-rules-read", cancellationToken).ConfigureAwait(false);
		context.Read("host-rules-read", hostRules);
		Require(!Boolean(hostRules, "allowPush"),
			"the host's own push gate still reads allow_push=true after the console write: " + Raw(hostRules));

		// The push is only offered to a pusher that can see the target: the game's own visibility gate
		// ("no-line-of-sight") drops the request before the rule is read. Put the two bodies in sight of
		// each other the way the other rows do, then read the member's own position as the before value.
		context.Note("member placed: " + await context.Session.BringTheMemberBesideTheHostAsync("the push half", cancellationToken).ConfigureAwait(false));

		// A body dropped on a tile keeps falling for a moment: the "the target did not move" half needs the
		// member to be standing still first, or the fall is read as a push (measured 2026-11-25: 2.6 units).
		var settled = await SettleAsync(context, cancellationToken).ConfigureAwait(false);
		context.Read("member settled", settled);

		var memberBefore = await context.MemberRecipeAsync("body-read", cancellationToken).ConfigureAwait(false);
		var (beforeX, beforeY) = Position(memberBefore);
		var pushStarted = DateTime.Now;
		var push = await context.RecipeAsync("push-player", cancellationToken, ("target", memberId)).ConfigureAwait(false);
		context.Read("push-player", push);
		Require(Boolean(push, "ok"), "the push gesture did not leave the host's client: " + Raw(push));
		var refused = await WaitForFreshLogLineAsync(
			context.HostDatedLogPath, "the host's allow_push rule is off", pushStarted, TimeSpan.FromSeconds(10), cancellationToken).ConfigureAwait(false);
		Require(refused is not null, "the refused push was not named on the pusher's own client");
		Require(refused is not null && refused.Contains("refusal.push_disabled_by_rule", StringComparison.Ordinal),
			"the refusal did not name the rule it came from: " + refused);
		await Task.Delay(TimeSpan.FromMilliseconds(1500), cancellationToken).ConfigureAwait(false);
		var memberAfter = await context.MemberRecipeAsync("body-read", cancellationToken).ConfigureAwait(false);
		var (afterX, afterY) = Position(memberAfter);
		Require(
			// A push moves the target by whole cells; a body standing on its own ground settles by a few
			// hundredths as it lands (measured 2026-11-26: before (2.043, 496.225), after (2.043, 496.279)
			// — 0.054 on a row where the push was correctly refused). Half a cell separates settling from
			// being moved.
			Math.Abs(afterX - beforeX) <= 0.5f && Math.Abs(afterY - beforeY) <= 0.5f,
			"the refused push still moved the target: before (" + beforeX.ToString("0.###", CultureInfo.InvariantCulture) + ", " + beforeY.ToString("0.###", CultureInfo.InvariantCulture)
				+ ") after (" + afterX.ToString("0.###", CultureInfo.InvariantCulture) + ", " + afterY.ToString("0.###", CultureInfo.InvariantCulture) + ")");

		// The push moved the target, so the pair can be out of sight again: put the member back beside the host
		// and let it stand still before the carry half, or the carry's own visibility gate drops it
		// (measured 2026-11-25 in a batch: the push half passed and the carry half read "no-line-of-sight").
		context.Note("member placed for the carry: " + await context.Session.BringTheMemberBesideTheHostAsync("the carry half", cancellationToken).ConfigureAwait(false));
		var hostAgain = await context.RecipeAsync("body-read", cancellationToken).ConfigureAwait(false);
		var (hostAgainX, hostAgainY) = Position(hostAgain);
		context.Read("member settled for the carry", await SettleAsync(context, cancellationToken).ConfigureAwait(false));

		// Standing beside the host is not the same as having it on screen: the game's visibility gate also needs the
		// target on the camera's side of the screen, so the member looks at the host before the carry gesture
		// (measured 2026-11-25: distance alone still read "no-line-of-sight").
		context.Read("member aims at the host", await context.MemberRecipeAsync("aim-at-world", cancellationToken,
			("x", hostAgainX.ToString(CultureInfo.InvariantCulture)),
			("y", hostAgainY.ToString(CultureInfo.InvariantCulture))).ConfigureAwait(false));

		// Row 2: a conscious body is not carryable by the game's own rule; the host's switch lifts exactly that,
		// and the relation forms on the member's client.
		await SetHostRuleAsync(context, "allow_always_carry", "true", cancellationToken).ConfigureAwait(false);
		var carryRules = await WaitForMemberRuleAsync(context, "allowAlwaysCarry", expected: true, cancellationToken).ConfigureAwait(false);
		Require(Boolean(carryRules, "allowAlwaysCarry"), "the member did not read the host's allow_always_carry=true: " + Raw(carryRules));

		var carry = await context.MemberRecipeAsync("carry-start", cancellationToken, ("mode", "carry"), ("target", "auto")).ConfigureAwait(false);
		context.Read("carry-start", carry);
		Require(Boolean(carry, "ok"), "the carry gesture did not leave the member's client: " + Raw(carry));
		// The host applies the relation and broadcasts the state; the carrier's own client learns about it a frame
		// or two later, so the reading is polled rather than taken once (2026-11-18: the first read said 0 while the
		// host log already read "starts carrying").
		JsonElement? carried = null;
		string? carries = null;
		for (var attempt = 0; attempt < 20; attempt++)
		{
			carried = await context.MemberRecipeAsync("carry-read", cancellationToken).ConfigureAwait(false);
			carries = Text(carried, "localCarries");
			if (carries is not null && carries != "0")
			{
				break;
			}

			await Task.Delay(TimeSpan.FromMilliseconds(250), cancellationToken).ConfigureAwait(false);
		}

		Require(carries is not null && carries != "0", "the carry the game itself refuses did not form under allow_always_carry: " + Raw(carried));
		await context.MemberRecipeAsync("carry-stop", cancellationToken, ("target", "auto")).ConfigureAwait(false);

		// Put the host's rules back: the next row must not inherit this one's switches.
		await SetHostRuleAsync(context, "allow_push", "true", cancellationToken).ConfigureAwait(false);
		await SetHostRuleAsync(context, "allow_always_carry", "false", cancellationToken).ConfigureAwait(false);
		// Both values must arrive; waiting on one of them read the other mid-flight (2026-11-18: allowAlwaysCarry
		// was still true when allowPush had already landed).
		JsonElement? restored = null;
		var restoredOk = false;
		for (var attempt = 0; attempt < 20; attempt++)
		{
			restored = await MemberRulesAsync(context, cancellationToken).ConfigureAwait(false);
			restoredOk = Boolean(restored, "allowPush") && !Boolean(restored, "allowAlwaysCarry");
			if (restoredOk)
			{
				break;
			}

			await Task.Delay(TimeSpan.FromMilliseconds(250), cancellationToken).ConfigureAwait(false);
		}

		Require(restoredOk, "the host's rules were not restored: " + Raw(restored));
	}

	/// <summary>The host writes one rule with the real console verb; the answer is the value in force.</summary>
	private static async Task SetHostRuleAsync(CaseContext context, string name, string value, CancellationToken cancellationToken)
	{
		var answer = await context.Session.ActionAsync(
			context.Session.HostUrl, "console", cancellationToken, ("-Text", "/gamerule " + name + " " + value)).ConfigureAwait(false);
		context.Read("gamerule " + name + "=" + value, answer);
		Require(Boolean(answer, "applied"), "the console refused '/gamerule " + name + " " + value + "': " + Raw(answer));
	}

	/// <summary>The member's rule surface, polled until <paramref name="expected"/> because the announcement is a frame.</summary>
	private static async Task<JsonElement?> WaitForMemberRuleAsync(CaseContext context, string property, bool expected, CancellationToken cancellationToken)
	{
		JsonElement? last = null;
		for (var wait = 0; wait < 20; wait++)
		{
			last = await MemberRulesAsync(context, cancellationToken).ConfigureAwait(false);
			if (Boolean(last, property) == expected)
			{
				return last;
			}

			await Task.Delay(TimeSpan.FromMilliseconds(250), cancellationToken).ConfigureAwait(false);
		}

		return last;
	}

	private static Task<JsonElement?> MemberRulesAsync(CaseContext context, CancellationToken cancellationToken) =>
		context.MemberRecipeAsync("host-rules-read", cancellationToken);

	/// <summary>The member's roster and one id, from the host's own member list (the same source the UI renders).</summary>
	/// <summary>Waits until the member's body stops moving, and answers the last reading it took.</summary>
	private static async Task<JsonElement?> SettleAsync(CaseContext context, CancellationToken cancellationToken)
	{
		JsonElement? last = null;
		var (lastX, lastY) = (double.NaN, double.NaN);
		for (var attempt = 0; attempt < 20; attempt++)
		{
			await Task.Delay(TimeSpan.FromMilliseconds(300), cancellationToken).ConfigureAwait(false);
			last = await context.MemberRecipeAsync("body-read", cancellationToken).ConfigureAwait(false);
			var (x, y) = Position(last);
			if (!double.IsNaN(lastX) && Math.Abs(x - lastX) < 0.1 && Math.Abs(y - lastY) < 0.1)
			{
				return last;
			}

			(lastX, lastY) = (x, y);
		}

		return last;
	}

	private static async Task<string> SingleMemberAsync(CaseContext context, CancellationToken cancellationToken)
	{
		for (var wait = 0; wait < 20; wait++)
		{
			var members = await context.Session.ReadMembersAsync(cancellationToken).ConfigureAwait(false);
			var ids = members.Where(id => !string.IsNullOrEmpty(id)).Distinct().ToArray();
			if (ids.Length == 1)
			{
				return ids[0];
			}

			await Task.Delay(TimeSpan.FromMilliseconds(250), cancellationToken).ConfigureAwait(false);
		}

		Require(false, "the host's member list never carried exactly one member");
		return string.Empty;
	}

	/// <summary>The local body's world point, from <c>body-read</c>'s own <c>localBody</c> object.</summary>
	private static (float X, float Y) Position(JsonElement? reading)
	{
		if (reading is { ValueKind: JsonValueKind.Object } state
			&& state.TryGetProperty("localBody", out var body)
			&& body.ValueKind == JsonValueKind.Object)
		{
			return ((float)body.GetProperty("positionX").GetDouble(), (float)body.GetProperty("positionY").GetDouble());
		}

		Require(false, "body-read did not carry the local body's position: " + Raw(reading));
		return (0f, 0f);
	}

	private static string Raw(JsonElement? reading) => reading is { } value ? value.GetRawText() : string.Empty;

	/// <summary>
	/// Waits for a line that carries <paramref name="needle"/> and was written at or after
	/// <paramref name="since"/>. The file offset a caller took before the gesture cannot be trusted: the host's
	/// log rotates under a long batch, and a rotated file makes the old offset point past the end (measured
	/// 2026-11-25, the refused push was on disk but the offset never matched it).
	/// </summary>
	private static async Task<string?> WaitForFreshLogLineAsync(string path, string needle, DateTime since, TimeSpan budget, CancellationToken cancellationToken)
	{
		var deadline = DateTime.UtcNow + budget;
		while (DateTime.UtcNow < deadline)
		{
			var log = ReadLog(path);
			var index = log.IndexOf(needle, StringComparison.Ordinal);
			while (index >= 0)
			{
				var lineStart = log.LastIndexOf('\n', index) + 1;
				var lineEnd = log.IndexOf('\n', index);
				var line = log[lineStart..(lineEnd < 0 ? log.Length : lineEnd)];
				if (LineWasWrittenSince(line, since))
				{
					return line;
				}

				index = log.IndexOf(needle, index + needle.Length, StringComparison.Ordinal);
			}

			await Task.Delay(TimeSpan.FromMilliseconds(250), cancellationToken).ConfigureAwait(false);
		}

		return null;
	}

	/// <summary>True when a log line's own <c>[yyyy-MM-dd HH:mm:ss.fff]</c> stamp is not older than the gesture.</summary>
	private static bool LineWasWrittenSince(string line, DateTime since)
	{
		var open = line.IndexOf('[', StringComparison.Ordinal);
		var close = open < 0 ? -1 : line.IndexOf(']', open + 1);
		if (open < 0 || close < 0)
		{
			return false;
		}

		var stamp = line[(open + 1)..close];
		return DateTime.TryParseExact(stamp, "yyyy-MM-dd HH:mm:ss.fff", CultureInfo.InvariantCulture, DateTimeStyles.None, out var written)
			&& written >= since.AddSeconds(-2);
	}

	private static async Task<string?> WaitForLogAsync(string path, long offset, string needle, TimeSpan budget, CancellationToken cancellationToken)
	{
		var deadline = DateTime.UtcNow + budget;
		while (DateTime.UtcNow < deadline)
		{
			var log = ReadLog(path);
			var index = log.IndexOf(needle, StringComparison.Ordinal);
			if (index >= offset)
			{
				var lineStart = log.LastIndexOf('\n', index) + 1;
				var lineEnd = log.IndexOf('\n', index);
				return log[lineStart..(lineEnd < 0 ? log.Length : lineEnd)];
			}

			await Task.Delay(TimeSpan.FromMilliseconds(250), cancellationToken).ConfigureAwait(false);
		}

		return null;
	}

	private static long LogLength(string path)
	{
		var info = new FileInfo(path);
		return info.Exists ? info.Length : 0;
	}

	private static string ReadLog(string path)
	{
		try
		{
			using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
			using var reader = new StreamReader(stream);
			return reader.ReadToEnd();
		}
		catch (IOException)
		{
			return string.Empty;
		}
	}

	private static bool Boolean(JsonElement? reading, string property) =>
		reading is { ValueKind: JsonValueKind.Object } state
		&& state.TryGetProperty(property, out var value)
		&& value.ValueKind == JsonValueKind.True;

	private static string? Text(JsonElement? reading, string property) =>
		reading is { ValueKind: JsonValueKind.Object } state
		&& state.TryGetProperty(property, out var value)
		&& value.ValueKind == JsonValueKind.String
			? value.GetString()
			: null;

	private static void Require(bool condition, string message)
	{
		if (!condition)
		{
			throw new InvalidOperationException(message);
		}
	}
}
