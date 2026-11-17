using System.Globalization;
using System.Text.Json;

namespace CasualtiesUnknownOnline.AcceptanceSuite.Cases;

/// <summary>
/// The per-player death count (the user's decision 2026-11-14). Three rows: a fresh character starts at
/// zero; two deaths with a revive between them read two, from the member's own answer and from the host's
/// answer about that member; and the count survives a save/continue, because it rides the character
/// snapshot the cut persists.
///
/// <para>
/// The deaths are driven through the game's own field (<c>Body.brainHealth</c>, the value <c>Body.alive</c>
/// reads) with the <c>body-force</c> recipe on the member's own client — the same edge a real kill produces,
/// without guessing at damage. It saves, continues and rejoins the member (tens of seconds), so it runs by
/// name and stays out of the batch whose wall time has a recorded baseline (rule 14).
/// </para>
/// </summary>
internal sealed class PlayerDeathsCase : AcceptanceCase
{
	public override string Name => "player-deaths";

	public override CaseCost Cost => CaseCost.NewWorld;

	/// <summary>Tens of seconds of save/continue; the batch's 299.8 s baseline is untouched by a by-name row.</summary>
	public override bool RunsInBatch => false;

	public override async Task RunAsync(CaseContext context, CancellationToken cancellationToken)
	{
		await context.Session.EnsureInWorldAsync(cancellationToken).ConfigureAwait(false);
		await context.Session.EnsureMemberAsync(cancellationToken).ConfigureAwait(false);
		var confirmedId = await context.Session.ConfirmedMemberIdAsync(cancellationToken).ConfigureAwait(false);
		Require(confirmedId is not null, "the suite could not confirm the member's id after the join");
		var memberId = confirmedId!;
		context.UseMember(memberId);

		// Row 3: the character this run spawned has not died. Both sides answer zero.
		var memberZero = await ConsoleAsync(context, context.Session.MemberUrl, "/deaths", cancellationToken).ConfigureAwait(false);
		Require(memberZero.Contains("0 time(s)", StringComparison.Ordinal), "a fresh character did not start at zero deaths: " + memberZero);
		var hostZero = await ConsoleAsync(context, context.Session.HostUrl, "/deaths", cancellationToken).ConfigureAwait(false);
		Require(hostZero.Contains("0 time(s)", StringComparison.Ordinal), "the host's fresh character did not start at zero deaths: " + hostZero);

		// Row 1, first death.
		await SetMemberAliveAsync(context, alive: false, cancellationToken).ConfigureAwait(false);
		var first = await ConsoleAsync(context, context.Session.MemberUrl, "/deaths", cancellationToken).ConfigureAwait(false);
		Require(first.Contains("1 time(s)", StringComparison.Ordinal), "the member's first death was not counted: " + first);

		// Row 1, the revive itself is not a death.
		await SetMemberAliveAsync(context, alive: true, cancellationToken).ConfigureAwait(false);
		var revived = await ConsoleAsync(context, context.Session.MemberUrl, "/deaths", cancellationToken).ConfigureAwait(false);
		Require(revived.Contains("1 time(s)", StringComparison.Ordinal), "a revive counted as another death: " + revived);

		// Row 1, second death.
		await SetMemberAliveAsync(context, alive: false, cancellationToken).ConfigureAwait(false);
		var second = await ConsoleAsync(context, context.Session.MemberUrl, "/deaths", cancellationToken).ConfigureAwait(false);
		Require(second.Contains("2 time(s)", StringComparison.Ordinal), "the member's second death was not counted: " + second);

		// Row 1's other half: the host reads the same count from the member's own report.
		var hostView = await ConsoleAsync(
			context,
			context.Session.HostUrl,
			"/deaths " + memberId,
			cancellationToken).ConfigureAwait(false);
		Require(hostView.Contains("2 time(s)", StringComparison.Ordinal), "the host did not read the member's two deaths from its report: " + hostView);

		// Row 2: the count is a fact of the character, so a cut carries it.
		var logOffset = LogLength(context.HostLogPath);
		var save = await context.Session.ActionAsync(context.Session.HostUrl, "console", cancellationToken, ("-Text", "/save")).ConfigureAwait(false);
		context.Read("save", save);
		var committed = await WaitForLogAsync(context.HostLogPath, logOffset, "Cut Command committed", TimeSpan.FromSeconds(25), cancellationToken).ConfigureAwait(false);
		Require(committed is not null, "the save cut did not commit");

		await context.Session.ActionAsync(context.Session.HostUrl, "leave-world", cancellationToken).ConfigureAwait(false);
		var left = false;
		for (var wait = 0; wait < 20 && !left; wait++)
		{
			await Task.Delay(TimeSpan.FromMilliseconds(500), cancellationToken).ConfigureAwait(false);
			var state = await context.Session.ActionAsync(context.Session.HostUrl, "state", cancellationToken).ConfigureAwait(false);
			left = !Boolean(state, "inWorld");
		}

		Require(left, "the host did not leave the world before continuing");

		var continueOffset = LogLength(context.HostLogPath);
		var continued = await context.Session.ActionAsync(context.Session.HostUrl, "continue-run", cancellationToken).ConfigureAwait(false);
		context.Read("continue", continued);
		var restored = await WaitForLogAsync(context.HostLogPath, continueOffset, "Continue restored world", TimeSpan.FromSeconds(40), cancellationToken).ConfigureAwait(false);
		Require(restored is not null, "the continue never restored the world");

		var inWorld = false;
		for (var wait = 0; wait < 30 && !inWorld; wait++)
		{
			await Task.Delay(TimeSpan.FromMilliseconds(500), cancellationToken).ConfigureAwait(false);
			var state = await context.Session.ActionAsync(context.Session.HostUrl, "state", cancellationToken).ConfigureAwait(false);
			inWorld = Boolean(state, "inWorld");
		}

		Require(inWorld, "the host did not enter the restored world");

		// The member leaves and rejoins through the host's own entry instruction: its restored character
		// (and the death count in it) is what the join hands over.
		await context.Session.ClearMemberSessionAsync(cancellationToken).ConfigureAwait(false);
		await context.Session.EnsureMemberAsync(cancellationToken).ConfigureAwait(false);
		await context.Session.WaitForMemberInCurrentRunAsync(cancellationToken).ConfigureAwait(false);
		var rejoinedId = await context.Session.ConfirmedMemberIdAsync(cancellationToken).ConfigureAwait(false);
		Require(rejoinedId is not null, "the suite could not confirm the member's id after the rejoin");
		var rejoinedMemberId = rejoinedId!;
		context.UseMember(rejoinedMemberId);

		var afterContinue = await ConsoleAsync(context, context.Session.MemberUrl, "/deaths", cancellationToken).ConfigureAwait(false);
		Require(afterContinue.Contains("2 time(s)", StringComparison.Ordinal), "the death count did not survive the save/continue: " + afterContinue);
		var hostAfterContinue = await ConsoleAsync(
			context,
			context.Session.HostUrl,
			"/deaths " + rejoinedMemberId,
			cancellationToken).ConfigureAwait(false);
		Require(hostAfterContinue.Contains("2 time(s)", StringComparison.Ordinal), "the host read a different count after the continue: " + hostAfterContinue);
	}

	/// <summary>
	/// Drive the member's own body through the game's <c>Body.brainHealth</c> field — the value
	/// <c>Body.alive</c> reads (<c>reversing/release-1.0.2/Assembly-CSharp/Body.cs:207-216</c>) — and wait
	/// for the derived flag, so the case reads the same edge the counter reads.
	/// </summary>
	private static async Task SetMemberAliveAsync(CaseContext context, bool alive, CancellationToken cancellationToken)
	{
		var forced = await context.MemberRecipeAsync(
			"body-force",
			cancellationToken,
			("sleep", "-1"),
			("consciousness", "-1"),
			("energy", "-1"),
			("brainHealth", alive ? "100" : "0"),
			("badSleepAmount", "-1"),
			("idleTime", "-1"),
			("intelligence", "-1"),
			("heartRate", "-1"),
			("bloodPressure", "-1"),
			("bloodOxygen", "-1"),
			("strength", "-1")).ConfigureAwait(false);
		Require(Boolean(forced, "ok"), "the member's body could not be forced " + (alive ? "alive" : "dead") + ": " + Raw(forced));
		Require(Boolean(forced, "alive") == alive, "the member's alive flag did not follow the forced brain health: " + Raw(forced));

		for (var wait = 0; wait < 20; wait++)
		{
			var body = await context.Session.ActionAsync(context.Session.MemberUrl, "body-read", cancellationToken).ConfigureAwait(false);
			if (Boolean(body, "alive") == alive)
			{
				context.Read(alive ? "member alive" : "member dead", body);
				return;
			}

			await Task.Delay(TimeSpan.FromMilliseconds(250), cancellationToken).ConfigureAwait(false);
		}

		Require(false, "the member's body never read alive=" + alive.ToString(CultureInfo.InvariantCulture));
	}

	/// <summary>Run one console line and return its answer line (the driver's console action reports the last line).</summary>
	private static async Task<string> ConsoleAsync(CaseContext context, string url, string text, CancellationToken cancellationToken)
	{
		var answer = await context.Session.ActionAsync(url, "console", cancellationToken, ("-Text", text)).ConfigureAwait(false);
		context.Read("console " + text, answer);
		Require(Boolean(answer, "applied"), "the console refused '" + text + "': " + Raw(answer));
		return Text(answer, "lastLine") ?? string.Empty;
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

	private static string Raw(JsonElement? reading) => reading?.GetRawText() ?? "<no reading>";

	private static void Require(bool condition, string message)
	{
		if (!condition)
		{
			throw new InvalidOperationException(message);
		}
	}
}
