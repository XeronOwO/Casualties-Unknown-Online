using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;

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
/// without guessing at damage. It saves, continues and rejoins the member, and that restore of tens of
/// seconds is the cost it declares.
/// </para>
/// </summary>
internal sealed class PlayerDeathsCase : AcceptanceCase
{
	public override string Name => "player-deaths";

	public override CaseCost Cost => CaseCost.NewWorld;

	/// <summary>Tens of seconds of save/continue; that restore is the cost it declares.</summary>

	public override async Task RunAsync(CaseContext context, CancellationToken cancellationToken)
	{
		// The suite's own setup already created the host's session and started this run: touching
		// EnsureInWorldAsync here would leave that world and start a second run (the driver's
		// contract is "the suite starts its run once, before the first case"), and the member's
		// rejoin into the restarted world does not fit the join budget (measured 2026-11-17).
		await context.Session.EnsureMemberAsync(cancellationToken).ConfigureAwait(false);
		var memberId = await SingleMemberAsync(context, cancellationToken).ConfigureAwait(false);
		context.UseMember(memberId);

		// Row 3: the character this run spawned has not died. Both sides answer zero.
		var memberZero = await ConsoleAsync(context, context.Session.MemberUrl, "/deaths", cancellationToken).ConfigureAwait(false);
		Require(ShowsDeaths(memberZero, 0), "a fresh character did not start at zero deaths: " + memberZero);
		var hostZero = await ConsoleAsync(context, context.Session.HostUrl, "/deaths", cancellationToken).ConfigureAwait(false);
		Require(ShowsDeaths(hostZero, 0), "the host's fresh character did not start at zero deaths: " + hostZero);

		// Row 1, first death.
		await SetMemberAliveAsync(context, alive: false, cancellationToken).ConfigureAwait(false);
		var first = await ConsoleAsync(context, context.Session.MemberUrl, "/deaths", cancellationToken).ConfigureAwait(false);
		Require(ShowsDeaths(first, 1), "the member's first death was not counted: " + first);

		// Row 1, the revive itself is not a death.
		await SetMemberAliveAsync(context, alive: true, cancellationToken).ConfigureAwait(false);
		var revived = await ConsoleAsync(context, context.Session.MemberUrl, "/deaths", cancellationToken).ConfigureAwait(false);
		Require(ShowsDeaths(revived, 1), "a revive counted as another death: " + revived);

		// Row 1, second death.
		await SetMemberAliveAsync(context, alive: false, cancellationToken).ConfigureAwait(false);
		var second = await ConsoleAsync(context, context.Session.MemberUrl, "/deaths", cancellationToken).ConfigureAwait(false);
		Require(ShowsDeaths(second, 2), "the member's second death was not counted: " + second);

		// Row 1's other half: the host reads the same count from the member's own report. The report is
		// throttled(1 Hz), so the host's answer is polled to the count rather than demanded instantly.
		var hostView = await HostDeathsAsync(context, memberId, 2, cancellationToken).ConfigureAwait(false);
		Require(ShowsDeaths(hostView, 2), "the host did not read the member's two deaths from its report: " + hostView);

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
		var rejoinedMemberId = await SingleMemberAsync(context, cancellationToken).ConfigureAwait(false);
		context.UseMember(rejoinedMemberId);

		// The restore lands on the restored body's first frames after the join instruction, so the member's
		// own answer is polled to the count instead of read in the same instant the join returns.
		var afterContinue = await MemberDeathsAsync(context, 2, cancellationToken).ConfigureAwait(false);
		Require(ShowsDeaths(afterContinue, 2), "the death count did not survive the save/continue: " + afterContinue);
		var hostAfterContinue = await HostDeathsAsync(context, rejoinedMemberId, 2, cancellationToken).ConfigureAwait(false);
		Require(ShowsDeaths(hostAfterContinue, 2), "the host read a different count after the continue: " + hostAfterContinue);
	}

	/// <summary>The member's own answer, polled to <paramref name="deaths"/> (the restore applies a few frames after the join).</summary>
	private static async Task<string> MemberDeathsAsync(CaseContext context, int deaths, CancellationToken cancellationToken)
	{
		var last = string.Empty;
		for (var wait = 0; wait < 40; wait++)
		{
			last = await ConsoleAsync(context, context.Session.MemberUrl, "/deaths", cancellationToken).ConfigureAwait(false);
			if (ShowsDeaths(last, deaths))
			{
				return last;
			}

			await Task.Delay(TimeSpan.FromMilliseconds(250), cancellationToken).ConfigureAwait(false);
		}

		return last;
	}

	/// <summary>
	/// The host's answer about one member, polled until it names <paramref name="deaths"/>: the count reaches
	/// the host in the member's throttled character report, so the first answer can still be the older count.
	/// </summary>
	private static async Task<string> HostDeathsAsync(CaseContext context, string memberId, int deaths, CancellationToken cancellationToken)
	{
		var last = string.Empty;
		for (var wait = 0; wait < 40; wait++)
		{
			last = await ConsoleAsync(context, context.Session.HostUrl, "/deaths " + memberId, cancellationToken).ConfigureAwait(false);
			if (ShowsDeaths(last, deaths))
			{
				return last;
			}

			await Task.Delay(TimeSpan.FromMilliseconds(250), cancellationToken).ConfigureAwait(false);
		}

		return last;
	}

	/// <summary>
	/// The host's roster names exactly one member — the member this case addresses. Read from the host's
	/// own answer rather than a log line: the roster is what the host's commands resolve a member through.
	/// </summary>
	private static async Task<string> SingleMemberAsync(CaseContext context, CancellationToken cancellationToken)
	{
		for (var wait = 0; wait < 20; wait++)
		{
			var members = await context.Session.ReadMembersAsync(cancellationToken).ConfigureAwait(false);
			if (members.Length == 1 && members[0].Length > 0)
			{
				return members[0];
			}

			await Task.Delay(TimeSpan.FromMilliseconds(250), cancellationToken).ConfigureAwait(false);
		}

		var final = await context.Session.ReadMembersAsync(cancellationToken).ConfigureAwait(false);
		Require(false, "the host's roster did not name exactly one member: " + string.Join(", ", final));
		return string.Empty;
	}

	/// <summary>
	/// The answer carries the count as a standalone number in the reader's own language (the catalogue
	/// renders "你在本局中已经死亡 {0} 次。" or "You have died {0} time(s) in this run."), so the number is
	/// the signal and the surrounding words are not asserted.
	/// </summary>
	private static bool ShowsDeaths(string line, int count) =>
		Regex.IsMatch(line, "(?<![0-9])" + count.ToString(CultureInfo.InvariantCulture) + "(?![0-9])");

	/// <summary>
	/// Drive the member's own body through the game's <c>Body.brainHealth</c> field — the value
	/// <c>Body.alive</c> reads (<c>reversing/release-1.0.2/Assembly-CSharp/Body.cs:207-216</c>) — and judge
	/// the derived flag from the recipe's own answer: there is no body-reading driver action, so the recipe
	/// IS the instrument (it reads the same body the counter ticks from).
	/// </summary>
	private static async Task SetMemberAliveAsync(CaseContext context, bool alive, CancellationToken cancellationToken)
	{
		JsonElement? last = null;
		for (var wait = 0; wait < 20; wait++)
		{
			last = await context.MemberRecipeAsync(
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
			if (Boolean(last, "ok") && Boolean(last, "alive") == alive)
			{
				context.Read(alive ? "member alive" : "member dead", last);
				return;
			}

			await Task.Delay(TimeSpan.FromMilliseconds(250), cancellationToken).ConfigureAwait(false);
		}

		Require(false, "the member's body never read alive=" + alive.ToString(CultureInfo.InvariantCulture) + ": " + Raw(last));
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
