using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.Json;
using CasualtiesUnknownOnline.AcceptanceSuite.Cases;

namespace CasualtiesUnknownOnline.AcceptanceSuite;

/// <summary>
/// The acceptance suite: one typed case per acceptance row, run against the real game. The suite
/// launches the game itself (or uses a session that is already up), drives it over the acceptance
/// channel, and judges only measured readings. Randomness is judged as a range, a visual row is not
/// automated, and a case that passes replaces the ticket it came from.
/// </summary>
internal static partial class Program
{
	private static async Task<int> Main(string[] args)
	{
		var options = Options.Parse(args);
		// Focused runs are the iteration loop: when the pair is already in a world, attach to it
		// instead of paying the cold setup again. The full batch stays fresh by default so one
		// case's leftovers cannot silently change the next judgement; --reuse opts the full batch
		// into the same fast path, --fresh forces a focused run back to a clean pair.
		var tryReuse = options.Reuse || (options.Suite != "all" && !options.Fresh);
		if (options.Problem is { } problem)
		{
			Console.Error.WriteLine(problem);
			Console.Error.WriteLine("usage: CasualtiesUnknownOnline.AcceptanceSuite [--list | --suite <name|all>] [--cost <InRun|NewWorld|ClientRestart>] [--reuse|--fresh] [--out <dir>] [--host <ws-url>] [--game-dir <dir>] [--member <id>]");
			return 64;
		}

		if (options.List)
		{
			foreach (var acceptanceCase in CaseRegistry.All)
			{
				Console.WriteLine($"{acceptanceCase.Name}\t{acceptanceCase.Launch}\t{acceptanceCase.Cost}");
			}

			return 0;
		}

		var selected = options.Suite == "all"
			? CaseRegistry.All
			: [.. CaseRegistry.All.Where(candidate => options.Suite.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Contains(candidate.Name, StringComparer.Ordinal))];
		if (options.Cost is { Length: > 0 } costFilter)
		{
			var costs = costFilter.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
			selected = [.. selected.Where(candidate => costs.Contains(candidate.Cost.ToString(), StringComparer.OrdinalIgnoreCase))];
		}

		if (selected.Count == 0)
		{
			Console.Error.WriteLine($"no case named '{options.Suite}'" + (options.Cost is { Length: > 0 } filter ? $" with cost '{filter}'" : string.Empty));
			return 64;
		}

		var outputDirectory = Path.GetFullPath(options.OutputDirectory ?? Path.Combine(RepositoryPaths.Root, ".acceptance", "suite"));
		Directory.CreateDirectory(outputDirectory);
		var results = new List<CaseResult>();
		var suiteClock = Stopwatch.StartNew();
		var sharedSetup = Stopwatch.StartNew();
		var session = new SessionDriver(options);
		// The per-case marks cover only the case's own window. Everything the pair writes during setup,
		// a restart, the gap between cases or the teardown would otherwise be invisible: the 2026-11-15
		// member log carried 184,942 TurretScript errors after the suite had already reported 0/0. Mark
		// the whole run before the pair starts, then subtract the case windows at the end.
		var suiteLogStart = await MarkSuiteLogsAsync(session, options).ConfigureAwait(false);
		(var sessionNote, var memberNote) = tryReuse
			? await TryReusePairAsync(session).ConfigureAwait(false)
			: await session.EnsurePairAsync(CancellationToken.None).ConfigureAwait(false);
		if (session.MemberRelaunches > 0)
		{
			memberNote += $" (the member was recreated {session.MemberRelaunches} time(s) to join: a ghost world it could not leave is a finding)";
		}
		// EnsureMemberAsync returns only after the host's roster names the member, so the normal path
		// reads that roster once. The handshake log tail is the fallback for an older host build or a
		// roster that trails the join; it is not a second confirmation on every run (measured
		// 2026-11-15: reading it after the pair was ready added 23.6 s).
		var members = await session.ReadMembersAsync(CancellationToken.None).ConfigureAwait(false);
		for (var wait = 0; wait < 30 && members.Length == 0; wait++)
		{
			await Task.Delay(TimeSpan.FromMilliseconds(500), CancellationToken.None).ConfigureAwait(false);
			members = await session.ReadMembersAsync(CancellationToken.None).ConfigureAwait(false);
		}

		if (members.Length == 0)
		{
			var confirmedMember = await session.ConfirmedMemberIdAsync(CancellationToken.None).ConfigureAwait(false);
			if (confirmedMember is not null)
			{
				members = [confirmedMember];
			}
		}

		if (members.Length > 0)
		{
			options = options with { MemberId = members[0] };
		}

		sharedSetup.Stop();
		Console.WriteLine($"session\t{sessionNote}");
		Console.WriteLine($"member\t{memberNote}");
		Console.WriteLine($"setup\t{sharedSetup.Elapsed.TotalSeconds:F1}s (shared pair)");
		foreach (var acceptanceCase in selected)
		{
			// The member's id is the HOST's view of it, and that view exists only once the handshake
			// and the member's own scene report have landed. Read it when the case needs it, not
			// once at startup: a read taken too early asked every medical case for --member.
			if (options.MemberId is null)
			{
				var currentMembers = await session.ReadMembersAsync(CancellationToken.None).ConfigureAwait(false);
				if (currentMembers.Length > 0)
				{
					options = options with { MemberId = currentMembers[0] };
				}
			}

			results.Add(await RunAsync(acceptanceCase, options, outputDirectory).ConfigureAwait(false));
			Console.WriteLine($"{acceptanceCase.Name}\t{(results[^1].Passed ? "PASS" : "FAIL")}\t{results[^1].Cost}\tsetup {results[^1].SetupMs / 1000:F1}s\tcase {results[^1].CaseMs / 1000:F1}s\ttotal {results[^1].TotalMs / 1000:F1}s\t{results[^1].Note}");
			if (string.Equals(Environment.GetEnvironmentVariable("CUO_SUITE_PROBE"), "1", StringComparison.Ordinal))
			{
				await ProbeFingerprintAsync(options, acceptanceCase.Name).ConfigureAwait(false);
			}
		}

		var suiteLogs = await ReadSuiteLogsAsync(session, options, suiteLogStart, results).ConfigureAwait(false);
		suiteClock.Stop();
		var reportPath = Path.Combine(outputDirectory, "report.md");
		await File.WriteAllTextAsync(reportPath, SuiteReport.Render(options, results, sharedSetup.Elapsed.TotalMilliseconds, suiteClock.Elapsed.TotalMilliseconds, suiteLogs)).ConfigureAwait(false);
		await SuiteReport.WriteJsonAsync(Path.Combine(outputDirectory, "report.json"), options, results, sharedSetup.Elapsed.TotalMilliseconds, suiteClock.Elapsed.TotalMilliseconds, suiteLogs).ConfigureAwait(false);
		Console.WriteLine($"report: {reportPath}");
		var passed = results.All(result => result.Passed) && suiteLogs.OutsideErrors == 0 && suiteLogs.Problem is null;
		if (suiteLogs.OutsideErrors > 0)
		{
			Console.WriteLine($"outside-case log errors: host {suiteLogs.HostOutside?.Errors ?? 0}, member {suiteLogs.MemberOutside?.Errors ?? 0}; see the report");
		}

		if (suiteLogs.Problem is { } logProblem)
		{
			Console.Error.WriteLine(logProblem);
		}

		return passed ? 0 : 1;
	}

	/// <summary>
	/// The focused-run fast path: attach to a pair that is already in a world instead of paying a
	/// cold world load. <c>--reuse</c> asks the full batch for the same path; <c>--fresh</c> turns it
	/// off. A pair that is not ready falls back to the normal setup and says so.
	/// </summary>
	private static async Task<(string HostNote, string MemberNote)> TryReusePairAsync(SessionDriver session)
	{
		try
		{
			return await session.ReusePairAsync(CancellationToken.None).ConfigureAwait(false);
		}
		catch (Exception exception) when (exception is DriverException or InvalidOperationException or IOException)
		{
			var pair = await session.EnsurePairAsync(CancellationToken.None).ConfigureAwait(false);
			return (pair.HostNote + $" (reuse failed: {exception.Message})", pair.MemberNote);
		}
	}

	/// <summary>
	/// Diagnostic (CUO_SUITE_PROBE=1): after each case, both clients re-capture the product's own
	/// block-table fingerprint and the two totals are printed, so the case that moves the host's
	/// table without the member's names itself.
	/// </summary>
	private static async Task ProbeFingerprintAsync(Options options, string label)
	{
		await using var host = new DriverClient(options.HostUrl);
		await host.ConnectAsync(CancellationToken.None).ConfigureAwait(false);
		await host.RecipeAsync("world-fingerprint", new Dictionary<string, string>(StringComparer.Ordinal), CancellationToken.None).ConfigureAwait(false);
		await using var member = new DriverClient(options.MemberUrl);
		await member.ConnectAsync(CancellationToken.None).ConfigureAwait(false);
		await member.RecipeAsync("world-fingerprint", new Dictionary<string, string>(StringComparer.Ordinal), CancellationToken.None).ConfigureAwait(false);
		var session = new SessionDriver(options);
		var hostTail = await session.ActionAsync(options.HostUrl, "log-tail", CancellationToken.None, ("-Argument", "[WorldFingerprint]"), ("-Text", "1")).ConfigureAwait(false);
		var memberTail = await session.ActionAsync(options.MemberUrl, "log-tail", CancellationToken.None, ("-Argument", "[WorldFingerprint]"), ("-Text", "1")).ConfigureAwait(false);
		Console.WriteLine($"probe\t{label}\thost={FingerprintTotal(hostTail)}\tmember={FingerprintTotal(memberTail)}");
	}

	private static string FingerprintTotal(JsonElement answer)
	{
		var line = answer is { ValueKind: JsonValueKind.Object } element
			&& element.TryGetProperty("line0", out var value)
			&& value.ValueKind == JsonValueKind.String
				? value.GetString() ?? string.Empty
				: string.Empty;
		var at = line.LastIndexOf("total ", StringComparison.OrdinalIgnoreCase);
		if (at < 0)
		{
			return "-";
		}

		var start = at + "total ".Length;
		var end = start;
		while (end < line.Length && char.IsLetterOrDigit(line[end]))
		{
			end++;
		}

		return end > start ? line[start..end] : "-";
	}

	private static async Task<CaseResult> RunAsync(AcceptanceCase acceptanceCase, Options options, string outputDirectory)
	{
		await using var host = new DriverClient(options.HostUrl);
		CaseContext? context = null;
		var connect = Stopwatch.StartNew();
		var run = new Stopwatch();
		var hostMark = -1L;
		var memberMark = -1L;
		string? markProblem = null;
		try
		{
			await host.ConnectAsync(CancellationToken.None).ConfigureAwait(false);
			connect.Stop();
			context = new CaseContext(options, outputDirectory, host, new SessionDriver(options));

			// The log mark is the case's own boundary: every warning/error after it is caused by this
			// case. A mark that cannot be read is a failed acceptance, not a zero count.
			try
			{
				hostMark = await context.Session.LogOffsetAsync(options.HostUrl, CancellationToken.None).ConfigureAwait(false);
			}
			catch (Exception exception) when (exception is DriverException or IOException or InvalidOperationException)
			{
				markProblem = $"host log could not be marked: {exception.Message}";
			}

			try
			{
				memberMark = await context.Session.LogOffsetAsync(options.MemberUrl, CancellationToken.None).ConfigureAwait(false);
			}
			catch (Exception exception) when (exception is DriverException or IOException or InvalidOperationException)
			{
				// A host-only case may legitimately have no member attached; the missing member count
				// is reported, never hidden, and the host count is still gated.
				memberMark = -1L;
				markProblem ??= $"member log could not be marked: {exception.Message}";
			}

			run.Start();
			await acceptanceCase.RunAsync(context, CancellationToken.None).ConfigureAwait(false);
			run.Stop();
			var setup = context.Session.SetupMilliseconds;
			var caseMs = Math.Max(0, run.Elapsed.TotalMilliseconds - setup);
			var (hostLogs, memberLogs, logProblem) = await ReadLogsAsync(context.Session, options, hostMark, memberMark, markProblem).ConfigureAwait(false);
			var logFailure = JudgeLogs(acceptanceCase, hostLogs, memberLogs, logProblem);
			return new CaseResult(
				acceptanceCase.Name,
				acceptanceCase.Cost,
				logFailure is null,
				logFailure ?? "ok",
				connect.Elapsed.TotalMilliseconds,
				setup,
				caseMs,
				context.Readings,
				context.Notes,
				hostLogs,
				memberLogs);
		}
		catch (Exception exception) when (exception is DriverException or AssertionException or IOException or InvalidOperationException)
		{
			connect.Stop();
			run.Stop();
			var setup = context?.Session.SetupMilliseconds ?? 0;
			var caseMs = Math.Max(0, run.Elapsed.TotalMilliseconds - setup);
			var (hostLogs, memberLogs, logProblem) = context is null
				? (null, null, (string?)null)
				: await ReadLogsAsync(context.Session, options, hostMark, memberMark, markProblem).ConfigureAwait(false);
			var note = exception.Message + (logProblem is null ? string.Empty : $" ({logProblem})");
			return new CaseResult(
				acceptanceCase.Name,
				acceptanceCase.Cost,
				false,
				note,
				connect.Elapsed.TotalMilliseconds,
				setup,
				caseMs,
				context?.Readings ?? new Dictionary<string, string>(StringComparer.Ordinal),
				context?.Notes ?? [],
				hostLogs,
				memberLogs);
		}
		finally
		{
			if (context is not null)
			{
				await context.DisposeAsync().ConfigureAwait(false);
			}
		}
	}

	private static async Task<(LogCounts? Host, LogCounts? Member, string? Problem)> ReadLogsAsync(
		SessionDriver session,
		Options options,
		long hostMark,
		long memberMark,
		string? existingProblem)
	{
		LogCounts? hostLogs = null;
		LogCounts? memberLogs = null;
		var problem = existingProblem;
		if (hostMark >= 0)
		{
			try
			{
				hostLogs = await session.LogSinceAsync(options.HostUrl, hostMark, CancellationToken.None).ConfigureAwait(false);
			}
			catch (Exception exception) when (exception is DriverException or IOException or InvalidOperationException)
			{
				problem ??= $"host log count failed: {exception.Message}";
			}
		}

		if (memberMark >= 0)
		{
			try
			{
				memberLogs = await session.LogSinceAsync(options.MemberUrl, memberMark, CancellationToken.None).ConfigureAwait(false);
			}
			catch (Exception exception) when (exception is DriverException or IOException or InvalidOperationException)
			{
				problem ??= $"member log count failed: {exception.Message}";
			}
		}

		return (hostLogs, memberLogs, problem);
	}

	private static async Task<(long Host, long Member, string? Problem)> MarkSuiteLogsAsync(SessionDriver session, Options options)
	{
		long hostMark = -1;
		long memberMark = -1;
		string? problem = null;
		try
		{
			hostMark = await session.LogOffsetAsync(options.HostUrl, CancellationToken.None).ConfigureAwait(false);
		}
		catch (Exception exception) when (exception is DriverException or IOException or InvalidOperationException)
		{
			problem = $"the suite could not mark the host log: {exception.Message}";
		}

		try
		{
			memberMark = await session.LogOffsetAsync(options.MemberUrl, CancellationToken.None).ConfigureAwait(false);
		}
		catch (Exception exception) when (exception is DriverException or IOException or InvalidOperationException)
		{
			problem ??= $"the suite could not mark the member log: {exception.Message}";
		}

		return (hostMark, memberMark, problem);
	}

	private static async Task<SuiteLogs> ReadSuiteLogsAsync(
		SessionDriver session,
		Options options,
		(long Host, long Member, string? Problem) start,
		IReadOnlyList<CaseResult> results)
	{
		var problem = start.Problem;
		LogCounts? hostTotal = null;
		LogCounts? memberTotal = null;
		if (start.Host >= 0)
		{
			try
			{
				hostTotal = await session.LogSinceAsync(options.HostUrl, start.Host, CancellationToken.None).ConfigureAwait(false);
			}
			catch (Exception exception) when (exception is DriverException or IOException or InvalidOperationException)
			{
				problem ??= $"the suite's host log count failed: {exception.Message}";
			}
		}

		if (start.Member >= 0)
		{
			try
			{
				memberTotal = await session.LogSinceAsync(options.MemberUrl, start.Member, CancellationToken.None).ConfigureAwait(false);
			}
			catch (Exception exception) when (exception is DriverException or IOException or InvalidOperationException)
			{
				problem ??= $"the suite's member log count failed: {exception.Message}";
			}
		}

		if (start.Host < 0 || start.Member < 0)
		{
			problem ??= "the suite could not mark both logs; setup and between-case errors cannot be proven absent";
		}

		var caseHostWarnings = results.Sum(result => result.HostLogs?.Warnings ?? 0);
		var caseHostErrors = results.Sum(result => result.HostLogs?.Errors ?? 0);
		var caseMemberWarnings = results.Sum(result => result.MemberLogs?.Warnings ?? 0);
		var caseMemberErrors = results.Sum(result => result.MemberLogs?.Errors ?? 0);
		var hostOutside = hostTotal is null
			? null
			: new LogCounts(
				Math.Max(0, hostTotal.Warnings - caseHostWarnings),
				Math.Max(0, hostTotal.Errors - caseHostErrors),
				hostTotal.FirstWarning,
				hostTotal.FirstError);
		var memberOutside = memberTotal is null
			? null
			: new LogCounts(
				Math.Max(0, memberTotal.Warnings - caseMemberWarnings),
				Math.Max(0, memberTotal.Errors - caseMemberErrors),
				memberTotal.FirstWarning,
				memberTotal.FirstError);
		return new SuiteLogs(hostTotal, memberTotal, hostOutside, memberOutside, problem);
	}

	private static string? JudgeLogs(AcceptanceCase acceptanceCase, LogCounts? hostLogs, LogCounts? memberLogs, string? problem)
	{
		if (problem is not null)
		{
			return problem;
		}

		if (hostLogs is null)
		{
			return "the host log count is unavailable; the case cannot prove it wrote no errors";
		}

		var errors = hostLogs.Errors + (memberLogs?.Errors ?? 0);
		var warnings = hostLogs.Warnings + (memberLogs?.Warnings ?? 0);
		if (errors > acceptanceCase.AllowedErrors)
		{
			return $"log errors: {errors} (allowed {acceptanceCase.AllowedErrors}); host: {hostLogs.FirstError}; member: {memberLogs?.FirstError}";
		}

		if (acceptanceCase.AllowedWarnings >= 0 && warnings > acceptanceCase.AllowedWarnings)
		{
			return $"log warnings: {warnings} (allowed {acceptanceCase.AllowedWarnings}); host: {hostLogs.FirstWarning}; member: {memberLogs?.FirstWarning}";
		}

		return null;
	}
}

internal sealed class CaseContext(Options options, string outputDirectory, DriverClient host, SessionDriver session) : IAsyncDisposable
{
	private readonly Options _options = options;
	private DriverClient? _member;
	private string? _memberId = options.MemberId;

	public DriverClient Host { get; } = host;
	public SessionDriver Session { get; } = session;

	public string OutputDirectory { get; } = outputDirectory;

	public List<string> Notes { get; } = [];

	public Dictionary<string, string> Readings { get; } = new(StringComparer.Ordinal);

	public string MemberId =>
		_memberId ?? throw new InvalidOperationException("the case needs --member (the id the host shows for the member)");

	/// <summary>Takes the member the host currently reports (a restart gives it a new session id).</summary>
	public void UseMember(string memberId) => _memberId = memberId;

	public async Task<DriverClient> MemberAsync(CancellationToken cancellationToken)
	{
		if (_member is null)
		{
			_member = new DriverClient(_options.MemberUrl);
			await _member.ConnectAsync(cancellationToken).ConfigureAwait(false);
		}

		return _member;
	}

	public async Task<JsonElement?> MemberLogTailAsync(string match, int count, CancellationToken cancellationToken) => await Session.LogTailAsync(Session.MemberUrl, match, count, cancellationToken).ConfigureAwait(false);

	public async Task<JsonElement?> MemberRecipeAsync(string name, CancellationToken cancellationToken, params (string Key, string Value)[] args)
	{
		var member = await MemberAsync(cancellationToken).ConfigureAwait(false);
		var dictionary = args.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal);
		return await member.RecipeAsync(name, dictionary, cancellationToken).ConfigureAwait(false);
	}

	public string HostLogPath =>
		_options.GameDir is { } gameDir
			? Path.Combine(gameDir, "BepInEx", "LogOutput.log")
			: throw new InvalidOperationException("the case needs --game-dir (the release install) to read the host log");

	public Task<JsonElement?> RecipeAsync(string name, CancellationToken cancellationToken, params (string Key, string Value)[] args)
	{
		var dictionary = args.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal);
		return Host.RecipeAsync(name, dictionary, cancellationToken);
	}

	/// <summary>
	/// The host's own body is the anchor every placement reads. The run's start answers before the
	/// body exists, and a case that read a zero position would place the member at the world origin.
	/// </summary>
	public async Task<JsonElement?> WaitForHostBodyAsync(CancellationToken cancellationToken)
	{
		var deadline = Stopwatch.StartNew();
		while (deadline.Elapsed < TimeSpan.FromSeconds(30))
		{
			var body = await RecipeAsync("body-read", cancellationToken).ConfigureAwait(false);
			if (body is { ValueKind: JsonValueKind.Object } element
				&& element.TryGetProperty("localBody", out var local)
				&& local.ValueKind == JsonValueKind.Object
				&& local.TryGetProperty("positionX", out var px) && px.TryGetDouble(out var x)
				&& local.TryGetProperty("positionY", out var py) && py.TryGetDouble(out var y)
				&& (x != 0 || y > 0))
			{
				return body;
			}

			await Task.Delay(TimeSpan.FromMilliseconds(500), cancellationToken).ConfigureAwait(false);
		}

		return null;
	}

	/// <summary>
	/// A scene that has just loaded projects the item rows before the objects exist; a give or a dose
	/// sent during that window answers ok and never reaches the body (measured: the custody line read
	/// `objects=0 mismatches=407`, then `objects=407 mismatches=0` seconds later). The case waits for
	/// the host's own custody line to report convergence AFTER the case started reading the log.
	/// </summary>
	/// <summary>Waits until the member's own client reports a spawned body (the seeded limbs live on it).</summary>
	public async Task WaitForMemberBodyAsync(CancellationToken cancellationToken)
	{
		for (var attempt = 0; attempt < 30; attempt++)
		{
			var state = await MemberRecipeAsync("body-read", cancellationToken).ConfigureAwait(false);
			if (state is { ValueKind: JsonValueKind.Object } body
				&& body.TryGetProperty("localBody", out var local)
				&& local.ValueKind == JsonValueKind.Object
				&& local.TryGetProperty("positionY", out var y)
				&& y.ValueKind == JsonValueKind.Number
				&& y.GetDouble() != 0)
			{
				return;
			}

			await Task.Delay(TimeSpan.FromMilliseconds(500), cancellationToken).ConfigureAwait(false);
		}

		throw new InvalidOperationException("the member's own body did not spawn");
	}
	public async Task WaitForCustodyAsync(CancellationToken cancellationToken)
	{
		var path = HostLogPath;
		if (!File.Exists(path))
		{
			return;
		}

		// The projection logs a clean line only on the transition to clean. A case that starts in an
		// already-converged world must read that line instead of waiting for a second one that will
		// never be written: the old wait cost 25 s per custody case. Only the LAST custody line of
		// this world decides — a mismatch after the clean line means the world moved again.
		var length = new FileInfo(path).Length;
		var from = Math.Max(Session.WorldStartLogOffset, length - 65536);
		var lastCustody = LastCustodyLine(ReadFrom(path, from));
		if (lastCustody is not null && lastCustody.Contains("mismatches=0", StringComparison.Ordinal))
		{
			return;
		}

		var offset = lastCustody is null ? Math.Max(Session.WorldStartLogOffset, length) : length;
		var deadline = Stopwatch.StartNew();
		while (deadline.Elapsed < TimeSpan.FromSeconds(25))
		{
			if (File.Exists(path) && new FileInfo(path).Length > offset)
			{
				var tail = LastCustodyLine(ReadFrom(path, offset));
				if (tail is not null)
				{
					if (tail.Contains("mismatches=0", StringComparison.Ordinal))
					{
						return;
					}

					offset = new FileInfo(path).Length;
				}
			}

			await Task.Delay(TimeSpan.FromMilliseconds(400), cancellationToken).ConfigureAwait(false);
		}
	}

	private static string? LastCustodyLine(string text)
	{
		var lines = text.Split('\n');
		for (var i = lines.Length - 1; i >= 0; i--)
		{
			if (lines[i].Contains("[ItemCustody]", StringComparison.Ordinal))
			{
				return lines[i];
			}
		}

		return null;
	}

	private static string ReadFrom(string path, long offset)
	{
		using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
		stream.Seek(offset, SeekOrigin.Begin);
		using var reader = new StreamReader(stream);
		return reader.ReadToEnd();
	}

	public void Read(string key, JsonElement? reading)
	{
		Readings[key] = reading is { } value ? value.GetRawText() : "null";
		Notes.Add($"{key} = {Readings[key]}");
	}

	public void Note(string text) => Notes.Add(text);

	/// <summary>
	/// Empties the player's body slots through the game's own drop entry, so a case that needs a
	/// fresh item has somewhere to put it. Every case that provides an item owns its own room.
	/// </summary>
	/// <summary>
	/// The nearest cell around the given point that the game's own damage path can change: block 0 is air and
	/// block 14 is the life pod's shell, whose BlockInfo row does not exist, so a damage call on it throws.
	/// The band reaches above the body as well — a host that spawned inside the pod's cave sees air below it
	/// and the shell on every side (measured 2026-11-13).
	/// </summary>
	public async Task<(int X, int Y, int Block)?> FindBreakableCellAsync(int startX, int startY, CancellationToken cancellationToken)
	{
		for (var y = startY + 24; y >= startY - 40; y--)
		{
			for (var offset = 0; offset <= 16; offset++)
			{
				foreach (var x in offset == 0 ? new[] { startX } : [startX - offset, startX + offset])
				{
					var read = await RecipeAsync("block-read-at", cancellationToken,
						("x", x.ToString(CultureInfo.InvariantCulture)),
						("y", y.ToString(CultureInfo.InvariantCulture))).ConfigureAwait(false);
					var block = read is { ValueKind: JsonValueKind.Object } element
						&& element.TryGetProperty("block", out var value) && value.ValueKind == JsonValueKind.Number
							? value.GetDouble()
							: (double?)null;
					if (block is not null && block.Value != 0 && block.Value != 14)
					{
						Read("scan " + x + "," + y, read);
						return (x, y, (int)block.Value);
					}
				}
			}
		}

		return null;
	}

	public async Task ClearBodyAsync(CancellationToken cancellationToken)
	{
		var bag = await RecipeAsync("body-items", cancellationToken).ConfigureAwait(false);
		Read("body-items", bag); if (bag is not { ValueKind: JsonValueKind.Object } element
			|| !element.TryGetProperty("ids", out var ids)
			|| ids.ValueKind != JsonValueKind.String)
		{
			return;
		}

		foreach (var entry in (ids.GetString() ?? string.Empty).Split('|', StringSplitOptions.RemoveEmptyEntries))
		{
			var slot = entry.Split(':', 2)[0];
			if (!int.TryParse(slot, out _))
			{
				continue;
			}

			await RecipeAsync("item-drop-hand", cancellationToken, ("slot", slot)).ConfigureAwait(false);
			await Task.Delay(TimeSpan.FromMilliseconds(250), cancellationToken).ConfigureAwait(false);
		}
	}

	/// <summary>
	/// The member's own bag, emptied through its client's drop entry: a member that survived an
	/// earlier case carries whatever it picked up, and a full bag refuses the next pickup.
	/// </summary>
	public async Task ClearMemberBodyAsync(CancellationToken cancellationToken)
	{
		var bag = await MemberRecipeAsync("body-items", cancellationToken).ConfigureAwait(false);
		Read("member-body-items", bag);
		if (bag is not { ValueKind: JsonValueKind.Object } element
			|| !element.TryGetProperty("ids", out var ids)
			|| ids.ValueKind != JsonValueKind.String)
		{
			return;
		}

		foreach (var entry in (ids.GetString() ?? string.Empty).Split('|', StringSplitOptions.RemoveEmptyEntries))
		{
			var slot = entry.Split(':', 2)[0];
			if (!int.TryParse(slot, out _))
			{
				continue;
			}

			await MemberRecipeAsync("item-drop-hand", cancellationToken, ("slot", slot)).ConfigureAwait(false);
			await Task.Delay(TimeSpan.FromMilliseconds(250), cancellationToken).ConfigureAwait(false);
		}
	}

	public async ValueTask DisposeAsync()
	{
		if (_member is not null)
		{
			await _member.DisposeAsync().ConfigureAwait(false);
		}
	}
}

internal abstract class AcceptanceCase
{
	public abstract string Name { get; }

	public virtual LaunchKind Launch => LaunchKind.Warm;

	/// <summary>
	/// What the case costs the suite. <see cref="CaseCost.InRun"/> cases verify inside one world and
	/// restore their own mutation; <see cref="CaseCost.NewWorld"/> cases need a fresh run or end the
	/// world; <see cref="CaseCost.ClientRestart"/> cases need a process (or a first-world character).
	/// The report groups by this value so a fast regression run can select only the cheap group.
	/// </summary>
	public virtual CaseCost Cost => CaseCost.InRun;

	/// <summary>
	/// The error lines this case is allowed to cause. Zero is the default: an error is a finding,
	/// and the case must fail even when its behavioural readings happened to pass.
	/// </summary>
	public virtual int AllowedErrors => 0;

	/// <summary>
	/// The warning lines this case is allowed to cause. A negative value means "report only": the
	/// count is visible in the report but not gated. A case with a known recurring warning must
	/// declare a finite budget and name the warning rather than filter it out.
	/// </summary>
	public virtual int AllowedWarnings => -1;

	public abstract Task RunAsync(CaseContext context, CancellationToken cancellationToken);
}

internal enum LaunchKind
{
	Warm,
	Fresh,
	Restart,
}

internal enum CaseCost
{
	InRun,
	NewWorld,
	ClientRestart,
}

internal sealed record CaseResult(
	string Name,
	CaseCost Cost,
	bool Passed,
	string Note,
	double ConnectMs,
	double SetupMs,
	double CaseMs,
	IReadOnlyDictionary<string, string> Readings,
	IReadOnlyList<string> Notes,
	LogCounts? HostLogs,
	LogCounts? MemberLogs)
{
	public double TotalMs => ConnectMs + SetupMs + CaseMs;
}

/// <summary>
/// The whole-run log count. The case rows cover only the case's own window; this row covers the
/// pair's setup, restarts, gaps, and teardown, where a stale object can throw without any case
/// owning it. <see cref="HostOutside"/> and <see cref="MemberOutside"/> are the totals minus the
/// windows the case rows counted; a non-zero outside error fails the suite.
/// </summary>
internal sealed record SuiteLogs(
	LogCounts? HostTotal,
	LogCounts? MemberTotal,
	LogCounts? HostOutside,
	LogCounts? MemberOutside,
	string? Problem)
{
	public int OutsideErrors => (HostOutside?.Errors ?? 0) + (MemberOutside?.Errors ?? 0);

	public int OutsideWarnings => (HostOutside?.Warnings ?? 0) + (MemberOutside?.Warnings ?? 0);
}

internal static class SuiteReport
{
	public static string Render(Options options, IReadOnlyList<CaseResult> results, double sharedSetupMs, double totalMs, SuiteLogs suiteLogs)
	{
		var builder = new StringBuilder();
		builder.AppendLine("# Acceptance suite report");
		builder.AppendLine();
		builder.AppendLine($"- Host: `{options.HostUrl}`");
		builder.AppendLine($"- Started: {DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss zzz}");
		builder.AppendLine($"- Wall time: {Seconds(totalMs)} s (shared pair setup {Seconds(sharedSetupMs)} s)");
		builder.AppendLine($"- Logs outside cases: host {Counts(suiteLogs.HostOutside)}, member {Counts(suiteLogs.MemberOutside)} — {(suiteLogs.OutsideErrors == 0 && suiteLogs.Problem is null ? "PASS" : "FAIL")}");
		builder.AppendLine();
		builder.AppendLine("| Case | Cost | Connect | Setup | Case | Total | Host W/E | Member W/E | Verdict | Note |");
		builder.AppendLine("|---|---|---|---|---|---|---|---|---|---|");
		foreach (var result in results)
		{
			builder.AppendLine($"| {result.Name} | {result.Cost} | {Seconds(result.ConnectMs)} | {Seconds(result.SetupMs)} | {Seconds(result.CaseMs)} | {Seconds(result.TotalMs)} | {Counts(result.HostLogs)} | {Counts(result.MemberLogs)} | {(result.Passed ? "PASS" : "FAIL")} | {Escape(result.Note)} |");
		}

		builder.AppendLine();
		builder.AppendLine($"**{results.Count(result => result.Passed)} passed, {results.Count(result => !result.Passed)} failed.**");
		builder.AppendLine();
		builder.AppendLine("## Logs outside cases");
		builder.AppendLine();
		builder.AppendLine($"- Whole run: host {Counts(suiteLogs.HostTotal)}, member {Counts(suiteLogs.MemberTotal)}");
		builder.AppendLine($"- Outside the case windows: host {Counts(suiteLogs.HostOutside)}, member {Counts(suiteLogs.MemberOutside)}; warnings {suiteLogs.OutsideWarnings}, errors {suiteLogs.OutsideErrors}");
		if (suiteLogs.Problem is { } suiteProblem)
		{
			builder.AppendLine($"- Log capture problem: {Escape(suiteProblem)}");
		}

		if (!string.IsNullOrEmpty(suiteLogs.HostOutside?.FirstError))
		{
			builder.AppendLine($"- Host first error (whole run): `{Escape(suiteLogs.HostOutside!.FirstError)}`");
		}

		if (!string.IsNullOrEmpty(suiteLogs.MemberOutside?.FirstError))
		{
			builder.AppendLine($"- Member first error (whole run): `{Escape(suiteLogs.MemberOutside!.FirstError)}`");
		}

		builder.AppendLine();
		builder.AppendLine("## Cost groups");
		builder.AppendLine();
		builder.AppendLine("| Cost | Cases | Passed | Connect | Setup | Case | Total |");
		builder.AppendLine("|---|---|---|---|---|---|---|");
		foreach (var group in results.GroupBy(result => result.Cost).OrderBy(group => group.Key))
		{
			builder.AppendLine($"| {group.Key} | {group.Count()} | {group.Count(result => result.Passed)} | {Seconds(group.Sum(result => result.ConnectMs))} | {Seconds(group.Sum(result => result.SetupMs))} | {Seconds(group.Sum(result => result.CaseMs))} | {Seconds(group.Sum(result => result.TotalMs))} |");
		}

		var slowest = results.OrderByDescending(result => result.TotalMs).Take(5).ToArray();
		if (slowest.Length > 0)
		{
			builder.AppendLine();
			builder.AppendLine("## Slowest cases");
			builder.AppendLine();
			foreach (var result in slowest)
			{
				builder.AppendLine($"- `{result.Name}` — total {Seconds(result.TotalMs)} s (connect {Seconds(result.ConnectMs)} s, setup {Seconds(result.SetupMs)} s, case {Seconds(result.CaseMs)} s)");
			}
		}

		foreach (var result in results)
		{
			builder.AppendLine();
			builder.AppendLine($"## {result.Name} — {(result.Passed ? "PASS" : "FAIL")}");
			builder.AppendLine($"- cost: `{result.Cost}`; connect `{Seconds(result.ConnectMs)}` s; setup `{Seconds(result.SetupMs)}` s; case `{Seconds(result.CaseMs)}` s; total `{Seconds(result.TotalMs)}` s");
			builder.AppendLine($"- logs: host {Counts(result.HostLogs)}; member {Counts(result.MemberLogs)}");
			if (!string.IsNullOrEmpty(result.HostLogs?.FirstError))
			{
				builder.AppendLine($"- host first error: `{Escape(result.HostLogs!.FirstError)}`");
			}

			if (!string.IsNullOrEmpty(result.MemberLogs?.FirstError))
			{
				builder.AppendLine($"- member first error: `{Escape(result.MemberLogs!.FirstError)}`");
			}

			foreach (var (key, value) in result.Readings)
			{
				builder.AppendLine($"- `{key}`: `{value}`");
			}

			foreach (var note in result.Notes)
			{
				builder.AppendLine($"- {note}");
			}
		}

		return builder.ToString();
	}

	public static async Task WriteJsonAsync(string path, Options options, IReadOnlyList<CaseResult> results, double sharedSetupMs, double totalMs, SuiteLogs suiteLogs)
	{
		var payload = new
		{
			suite = options.Suite,
			host = options.HostUrl,
			startedAt = DateTimeOffset.Now,
			totalMs,
			sharedSetupMs,
			logsOutsideCases = new
			{
				hostTotal = suiteLogs.HostTotal,
				memberTotal = suiteLogs.MemberTotal,
				hostOutside = suiteLogs.HostOutside,
				memberOutside = suiteLogs.MemberOutside,
				outsideErrors = suiteLogs.OutsideErrors,
				outsideWarnings = suiteLogs.OutsideWarnings,
				problem = suiteLogs.Problem,
			},
			cases = results.Select(result => new
			{
				name = result.Name,
				cost = result.Cost.ToString(),
				verdict = result.Passed ? "PASS" : "FAIL",
				note = result.Note,
				connectMs = result.ConnectMs,
				setupMs = result.SetupMs,
				caseMs = result.CaseMs,
				totalMs = result.TotalMs,
				hostWarnings = result.HostLogs?.Warnings,
				hostErrors = result.HostLogs?.Errors,
				memberWarnings = result.MemberLogs?.Warnings,
				memberErrors = result.MemberLogs?.Errors,
				hostFirstError = result.HostLogs?.FirstError,
				memberFirstError = result.MemberLogs?.FirstError,
			}),
		};
		await File.WriteAllTextAsync(path, JsonSerializer.Serialize(payload, new JsonSerializerOptions { WriteIndented = true })).ConfigureAwait(false);
	}

	private static string Counts(LogCounts? counts) => counts is null ? "-" : $"{counts.Warnings}/{counts.Errors}";

	private static string Seconds(double milliseconds) => (milliseconds / 1000).ToString("F1", CultureInfo.InvariantCulture);

	private static string Escape(string text) => text.Replace("|", "\\|", StringComparison.Ordinal).ReplaceLineEndings(" ");
}

internal sealed record Options
{
	public bool List { get; init; }

	public string Suite { get; init; } = "all";

	public string? Cost { get; init; }

	public bool Reuse { get; init; }

	public bool Fresh { get; init; }

	public string HostUrl { get; init; } = "ws://127.0.0.1:18590";

	public string MemberUrl { get; init; } = "ws://127.0.0.1:18591";

	public string? OutputDirectory { get; init; }

	public string? GameDir { get; init; }

	public string? MemberId { get; init; }

	public string? Problem { get; init; }

	public static Options Parse(string[] args)
	{
		var options = new Options();
		for (var index = 0; index < args.Length; index++)
		{
			switch (args[index])
			{
				case "--list":
					options = options with { List = true };
					break;
				case "--suite" when index + 1 < args.Length:
					options = options with { Suite = args[++index] };
					break;
				case "--cost" when index + 1 < args.Length:
					options = options with { Cost = args[++index] };
					break;
				case "--reuse":
					options = options with { Reuse = true };
					break;
				case "--fresh":
					options = options with { Fresh = true };
					break;
				case "--out" when index + 1 < args.Length:
					options = options with { OutputDirectory = args[++index] };
					break;
				case "--host" when index + 1 < args.Length:
					options = options with { HostUrl = args[++index] };
					break;
				case "--member-host" when index + 1 < args.Length:
					options = options with { MemberUrl = args[++index] };
					break;
				case "--game-dir" when index + 1 < args.Length:
					options = options with { GameDir = args[++index] };
					break;
				case "--member" when index + 1 < args.Length:
					options = options with { MemberId = args[++index] };
					break;
				default:
					return new Options { Problem = $"unknown argument '{args[index]}'" };
			}
		}

		return options;
	}
}

internal static class CaseRegistry
{
	public static IReadOnlyList<AcceptanceCase> All { get; } =
	[
		// Run before any case moves the world: the two entry tables are the generation's own output,
		// and a later case's exploration tears the comparison down to a time-dependent reading.
		new WorldFingerprintCase(),
		// Parked (2026-11-12): the starting kit is granted when a character FIRST enters a world, and
		// this fixture reuses one long-lived session — the member's kit is long consumed by the time
		// any case runs (measured: 0 items; 2026-11-15: [emergencylight] only). The row needs a
		// character that has never entered a world, which means its own session; it stays out of the
		// all-suite run until the suite owns a per-case launch. The reading is on
		// docs/backlog/in-progress/1.0-acceptance-suite.md.
		// new StartingSuppliesCase(), // parked: needs a first-world-entry character
		new MedicalChartCase(),
		new ItemRestingsCase(),
		new ItemCustomizationCase(),
		new GiveItemCase(),
		new OverlaySoundCase(),
		new WorldsPageCase(),
		new SettingsPageCase(),
		new ConsoleCompletionCase(), // the console offers only the commands the player can run
		new WorldClockCase(), // the run's clock is the host's: a requested speed reaches the member
		new PauseClockCase(), // a pause is the client's own: the host's clock stops, the member's keeps running
		new CloneProbeCase(),
		new SpectatorPresentationCase(), // the watcher's screen/HUD/audio are the watched body's, not the local one
		new TraderCase(),
		new DrinkCase(), // the drink family: water is drunk through the game's own path, the refuse type is refused
		// (a row the sweep lists under the unhooked item families).
		new LayerPanelCase(),
		// new CraftingCase(), // parked 2026-11-15: the host's starting materials are consumed by an earlier
		// case, so the isolated read is `craftable 0`, and in the all-suite run the first craftable recipe
		// had already been made (`madeBefore=true`, count stayed 1). The row needs a fresh character (or a
		// game-native restock), which is the same per-case-launch gap as starting-supplies. Reading is on
		// docs/backlog/in-progress/1.0-acceptance-suite.md.
		new WorldCensusCase(),
		// new CraftPanelCase(), // parked 2026-11-13 02:02: Escape first does not help: the throw comes from the recipe's own
		// direct call to PlayerCamera.OpenCraftScreen (a reflection-wrapped exception), not from the CUO window being up. crafting-panel mode=open throws inside the game's own
		// PlayerCamera.OpenCraftScreen (runtimeException, no detail) while the CUO window is up — the panel open needs the
		// window closed first; the reading is on the suite ticket. // the host's own generated world: its building objects, counted by prefab // the crafting entry the game itself calls: a starting-material recipe is made and the product count grows // the end-of-layer panel as objects: inactive mid-layer on the host, no choice shown on the member
		// (a row the sweep lists under the unhooked item families).
		new ProstheticCase(),
		new RunSettingsCase(),
		// new RadiationLineCase(), // parked: the line activates only when layerTimeSpent exceeds
		// timelimit * 60 (WorldGeneration.cs:986-988), and the fixture cannot switch it off again;
		// the reading is on the suite ticket.
		new TopicalDoseCase(),
		// new WorldFingerprintCase(), // parked 2026-11-12 with the divergence reading: at world entry
		// both clients read the same eight chunks (9D1810AC432FBA29 …), and after the suite's run the
		// host's fresh table reads F9BEE3A2FE68F478 … (total 3530B1BB79FB4441) while the member's still
		// reads the entry state (total 90D3159D4F782ADA) — the mutation did not reach the member.
		// the block and eat rows run after the heal: both drive the member's own client and the heal's
		// seed needs the member's body untouched (the order effect measured 2026-11-13 02:30).
		// (parked note kept below: the case searches every solid cell within two cells
		// of the host (twelve found) and the game's own break path changes none of them — every cell
		// reads `block 14, damage -1`, the life pod's own indestructible shell. The row needs a fixture
		// that reaches breakable ground (a cell whose `damage` is not -1), not another cell search.
		// Reading on 1.0-acceptance-suite.md. // parked 2026-11-12: the block-hit recipe now names its stage, and the
		// damage stage still throws a NullReferenceException at a cell block-read-at calls solid;
		// the case also read a host at the menu (inWorld:false) — the row needs the in-world
		// precondition before the damage path is re-read. Reading on 1.0-acceptance-suite.md. // parked: the cell under the player is air, and world.DamageBlock on an
		// air cell throws a NullReferenceException inside the game (2026-11-12); the row needs the case
		// to find a solid cell around the host before it hits.
		new ConsoleHealCase(), // un-parked 2026-11-13 01:50 with the member's own trace beside the reading; re-parked 2026-11-13 01:30: the full-suite run read skinHealth 0.334, pain 83.36,
		// dislocated true after the host's /heal @a answered applied:true — the reset lands and the seeded wound's own
		// tick re-damages before the read. The earlier 17/17 pass was a single race, not a fix. Reading on the suite ticket.
		// un-parked 2026-11-13: the suite owns the member now (the stale-session
		// reading of 2026-11-12 judged a member the host's world never adopted); the row's judge is
		// the member's own limb after the host's `/heal @a`.
		// own limb stays partial in an aged session; the reading is on the suite ticket.
		new EatSoundCase(), // a host's own eat plays on the member's machine
		new BlockSyncCase(), // a break the host applies reaches the member's own block table, and the reverse
		new WorldFingerprintCase("world-fingerprint-after-mutation"), // both tables still agree after both breaks
		new BlockIdleDriftCase(), // the two tables agree and stay agreed while neither body moves (the divergence probe)
		new BlockReconcileCase(), // a host write whose live message was lost reaches the member on the 60 s reconcile
		new MineBlastCase(), // a mine the host fires destroys a building on the member's own copy
		// save-continue leaves the world and restores it: it goes after every case that only needs
		// the world the suite already owns.
		new EnemyPairingCase(), // parked 2026-11-12: the member adopted the host's 47 rows with 22
		// unresolved and aligned=False — review/1.0-enemy-roster-adopts-unresolved-rows.md. // parked 2026-11-12: the member scene holds 791 animals where the host
		// holds 39 — the block-table divergence on a second surface (review/1.0-block-table-diverges-between-clients.md).
		new EnemyKillCase(), // a host's kill cuts the member's own animal count (judged as a fall, not arithmetic)
		new RadiationLineCase(),
		// The host's unchipped preset reaches the member's WorldGeneration.Start, not just its
		// dictionary. It restarts into an unchipped run and leaves that world to the next new-world
		// case (it no longer restarts twice): every case after it is also NewWorld.
		new LateLayerSwitchesCase(),
		// The drill pod is the native two-layer trigger: it waits for every living member to stand at
		// the pod before the host's descent, and refuses (with the missing names) when one is away.
		new PodTeamWaitCase(),
		// The last layer's own gate: both clients generate layer 10 of an unchipped run,
		// the cutscene clears each client's local blockItemUsage, and each client's own
		// staged attack moves its own native attack state.
		new LastLayerGateCase(),
		new SaveContinueCase(),
		new RemoteRemovalCase(), // the member leaves: the host's displayed body goes and its own log says so
		new RoleClearingCase(), // the host ends its run: the member drops its role and rejoins a new session without a restart
		new RunEndCase(), // the host leaves the world: the member leaves it too (run-end broadcast)
		// Parked 2026-11-14 09:58: with the pointer held on the band every tick the case opens the lock
		// (three single runs passed), but inside the full order the game's own progress fills slowly
		// (0.742 in twenty seconds at an angle 24 degrees off) and the case's poll ends first. The row
		// needs a longer, angle-corrected hold; the case carries the readings in `LockpickCase.cs`.
		// new LockpickCase(),
		// The session picker unlocks all three portraits and the session's ending must not write the
		// host's or member's solo `wonchar` keys. It ends its own run; the ending case follows with a
		// fresh world.
		new CharacterChoiceCase(),
		// The ending ends the run: it goes last, after every case that needs a world.
		new EndingCase(),
	];
}

internal sealed class AssertionException(string message) : Exception(message);













