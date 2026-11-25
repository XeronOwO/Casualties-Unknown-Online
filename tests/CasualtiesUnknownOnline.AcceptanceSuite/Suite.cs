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
		// The manual driver lives in the suite's own action client now (the port of drive-in-process.ps1):
		// `--action`-style probes are `drive --url ... --action ...` here, and nothing about them is shell.
		if (args.Length > 0 && string.Equals(args[0], "drive", StringComparison.Ordinal))
		{
			return await DriveCommand.RunAsync(args[1..]).ConfigureAwait(false);
		}

		var options = Options.Parse(args);
		// Focused runs are the iteration loop: when the pair is already in a world, attach to it
		// instead of paying the cold setup again. The full batch stays fresh by default so one
		// case's leftovers cannot silently change the next judgement; --reuse opts the full batch
		// into the same fast path, --fresh forces a focused run back to a clean pair.
		var tryReuse = options.Reuse || (options.Suite != "all" && !options.Fresh);
		if (options.Problem is { } problem)
		{
			Console.Error.WriteLine(problem);
			Console.Error.WriteLine("usage: CasualtiesUnknownOnline.AcceptanceSuite [--list | --suite <name|all>] [--cost <InRun|NewWorld|ClientRestart>] [--reuse|--fresh] [--parallel-setup|--serial-setup] [--out <dir>] [--host <ws-url>] [--game-dir <dir>] [--member <id>] [--alt <ws-url>]");
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

		// Acceptance is one run over every registered case, in the registry's order: a case that needs a
		// world, a restart or a first-world character declares that in Cost and the runner pays it there.
		IReadOnlyList<AcceptanceCase> selected = options.Suite == "all"
			? [.. CaseRegistry.All]
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
		var suiteStartedUtc = DateTimeOffset.UtcNow;
		var results = new List<CaseResult>();
		var suiteClock = Stopwatch.StartNew();
		var sharedSetup = Stopwatch.StartNew();
		var timeline = new Timeline();
		timeline.Begin("suite");
		// Opened before the first case drives a client: an exit code cannot be read from a process that
		// is already gone, and the 2026-11-20 A3 member died between two socket calls (see the ledger).
		var clientExits = new ClientExitLedger(options.PidsPath);
		clientExits.Refresh();
		options = options with { ClientExits = clientExits };
		var session = new SessionDriver(options) { Timeline = timeline.Step };
		// The per-case marks cover only the case's own window. Everything the pair writes during setup,
		// a restart, the gap between cases or the teardown would otherwise be invisible: the 2026-11-15
		// member log carried 184,942 TurretScript errors after the suite had already reported 0/0. Mark
		// the whole run before the pair starts, then subtract the case windows at the end.
		var suiteLogStart = await MarkSuiteLogsAsync(session, options).ConfigureAwait(false);
		timeline.Begin("setup");
		var pairStartedUtc = DateTimeOffset.UtcNow;
		// A client that draws placeholders makes every later reading meaningless, so the run refuses to
		// start rather than spending twenty minutes proving nothing: the message names the client and the
		// label count, and the exit stays a clean refusal (measured 2026-11-22: the guest's preference
		// named a language file that was not on disk).
		string sessionNote;
		string memberNote;
		try
		{
			(sessionNote, memberNote) = tryReuse
				? await TryReusePairAsync(session).ConfigureAwait(false)
				: await session.EnsurePairAsync(CancellationToken.None).ConfigureAwait(false);
		}
		catch (DriverException refusal) when (refusal.Message.Contains("draws placeholders", StringComparison.Ordinal))
		{
			Console.WriteLine($"FAIL the run cannot start: {refusal.Message}");
			return 2;
		}
		var pairReadyUtc = DateTimeOffset.UtcNow;
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

		timeline.Step($"member id {options.MemberId ?? "(none)"}; pair setup {sharedSetup.Elapsed.TotalSeconds:F1}s");
		timeline.End("setup");
		sharedSetup.Stop();
		Console.WriteLine($"session\t{sessionNote}");
		Console.WriteLine($"member\t{memberNote}");
		Console.WriteLine($"setup\t{sharedSetup.Elapsed.TotalSeconds:F1}s (shared pair)");
		WriteProgress(outputDirectory, $"suite\tstart\t{selected.Count} case(s)");
		var runChangedByTheLastRow = false;
		var leftByTheLastRow = CaseLeftovers.None;
		var previousCase = "the pair the batch started on";
		// The rules the batch starts with are the baseline a `Rules` rollback restores: read it before the first row can
		// write a switch. A row's own restore is skipped when it throws, so a failed row used to leave `allow_push`
		// changed for every later row and for the next batch (measured 2026-11-25, `HostRuleTogglesCase.cs:35-37`).
		var rulesBaseline = await session.ReadRuleSwitchesAsync(CancellationToken.None).ConfigureAwait(false);
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

			WriteProgress(outputDirectory, $"case\tstart\t{acceptanceCase.Name}");
			results.Add(await RunAsync(acceptanceCase, options, outputDirectory, timeline, runChangedByTheLastRow, leftByTheLastRow, previousCase, rulesBaseline).ConfigureAwait(false));
			// A row that continued a save, restarted the host or changed the run leaves a world the next
			// row did not start in: the runner starts a fresh run before that next row.
			runChangedByTheLastRow = acceptanceCase.LeavesTheRunChanged;
			// What the row changed without putting back travels with it into the next row's setup, which
			// undoes exactly that and proves it before the row's own body runs.
			leftByTheLastRow = acceptanceCase.Leaves;
			previousCase = acceptanceCase.Name;
			WriteProgress(outputDirectory, $"case\t{(results[^1].Passed ? "PASS" : "FAIL")}\t{acceptanceCase.Name}\t{results[^1].TotalMs / 1000:F1}s");
			if (!results[^1].Passed)
			{
				await TrySaveHostTailAsync(session, options, outputDirectory, acceptanceCase.Name).ConfigureAwait(false);
			}
			Console.WriteLine($"{acceptanceCase.Name}\t{(results[^1].Passed ? "PASS" : "FAIL")}\t{results[^1].Cost}\tsetup {results[^1].SetupMs / 1000:F1}s\tcase {results[^1].CaseMs / 1000:F1}s\ttotal {results[^1].TotalMs / 1000:F1}s\t{results[^1].Note}");
			if (string.Equals(Environment.GetEnvironmentVariable("CUO_SUITE_PROBE"), "1", StringComparison.Ordinal))
			{
				await ProbeFingerprintAsync(options, acceptanceCase.Name).ConfigureAwait(false);
			}
		}

		var suiteLogs = await ReadSuiteLogsAsync(session, options, suiteLogStart, results).ConfigureAwait(false);
		suiteClock.Stop();
		var suiteFinishedUtc = DateTimeOffset.UtcNow;
		var passed = results.All(result => result.Passed) && suiteLogs.OutsideErrors == 0 && suiteLogs.Problem is null;
		timeline.End("suite", passed ? "ok" : "fail");
		var timelinePath = Path.Combine(outputDirectory, "timeline.txt");
		await File.WriteAllLinesAsync(timelinePath, timeline.Lines).ConfigureAwait(false);
		var reportPath = Path.Combine(outputDirectory, "report.md");
		await File.WriteAllTextAsync(reportPath, SuiteReport.Render(options, results, sharedSetup.Elapsed.TotalMilliseconds, suiteClock.Elapsed.TotalMilliseconds, suiteLogs, suiteStartedUtc, suiteFinishedUtc, pairStartedUtc, pairReadyUtc, timeline)).ConfigureAwait(false);
		await SuiteReport.WriteJsonAsync(Path.Combine(outputDirectory, "report.json"), options, results, sharedSetup.Elapsed.TotalMilliseconds, suiteClock.Elapsed.TotalMilliseconds, suiteLogs, suiteStartedUtc, suiteFinishedUtc, pairStartedUtc, pairReadyUtc, timeline).ConfigureAwait(false);
		Console.WriteLine($"report: {reportPath}");
		Console.WriteLine($"timeline: {timelinePath}");
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
	/// A run of every case takes long enough that someone has to be able to see where it is while it
	/// runs: the console is buffered whenever the harness captures it, so each case announces itself in
	/// progress.log as well. Appending and closing per line is what makes it readable from outside.
	/// </summary>
	private static void WriteProgress(string outputDirectory, string line)
	{
		try
		{
			File.AppendAllText(
				Path.Combine(outputDirectory, "progress.log"),
				string.Format(CultureInfo.InvariantCulture, "{0:HH:mm:ss.fff}\t{1}{2}", DateTimeOffset.Now, line, Environment.NewLine));
		}
		catch (IOException)
		{
			// Progress is a courtesy for whoever watches the run; a locked file must not stop the suite.
		}
	}

	/// <summary>
	/// A failed row takes the host's own log with it: <c>latest.log</c> rotates on every client
	/// restart, so a reading taken after the run has lost the lines that explain the failure — the
	/// save rows failed with "the cut did not commit" and the cause was already gone when the run
	/// ended. Names are kept apart per row so a later row cannot overwrite the first failure's log.
	/// </summary>
	private static async Task TrySaveHostTailAsync(
		SessionDriver session,
		Options options,
		string outputDirectory,
		string caseName)
	{
		try
		{
			var tail = await session.LogTailAsync(options.HostUrl, string.Empty, 6000, CancellationToken.None).ConfigureAwait(false);
			var path = Path.Combine(outputDirectory, $"host-log-{caseName}.json");
			await File.WriteAllTextAsync(path, tail?.ToString() ?? "(the host answered no log lines)").ConfigureAwait(false);

			// The member's own log too: a row can fail on an exception that only the member's client raised
			// (measured 2026-11-25: run-settings failed on a member-side NullReferenceException and the report
			// carried the host's log only).
			try
			{
				var memberTail = await session.LogTailAsync(options.MemberUrl, string.Empty, 6000, CancellationToken.None).ConfigureAwait(false);
				await File.WriteAllTextAsync(
					Path.Combine(outputDirectory, $"member-log-{caseName}.json"),
					memberTail?.ToString() ?? "(the member answered no log lines)").ConfigureAwait(false);
			}
			catch (Exception memberException) when (memberException is DriverException or IOException or InvalidOperationException)
			{
				await File.WriteAllTextAsync(
					Path.Combine(outputDirectory, $"member-log-{caseName}.json"),
					"the member log could not be copied: " + memberException.Message).ConfigureAwait(false);
			}
		}
		catch (Exception exception) when (exception is DriverException or IOException or InvalidOperationException)
		{
			try
			{
				await File.WriteAllTextAsync(
					Path.Combine(outputDirectory, $"host-log-{caseName}.json"),
					"the host log could not be copied: " + exception.Message).ConfigureAwait(false);
			}
			catch (IOException)
			{
				// Nothing more to do: the row's own reading is still in the report.
			}
		}
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

	private static async Task<CaseResult> RunAsync(
		AcceptanceCase acceptanceCase,
		Options options,
		string outputDirectory,
		Timeline timeline,
		bool runChangedByTheLastRow,
		CaseLeftovers leftByTheLastRow,
		string previousCase,
		IReadOnlyDictionary<string, string> rulesBaseline)
	{
		var startedUtc = DateTimeOffset.UtcNow;
		timeline.Begin(acceptanceCase.Name);
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
			var caseSession = new SessionDriver(options) { Timeline = timeline.Step };
			context = new CaseContext(options, outputDirectory, host, caseSession, timeline);
			// The runner owns the world's lifetime: a case that verifies inside a world must find one,
			// whoever left the world last. Measured 2026-11-21: `character-solo-lock` left it as its
			// own verdict and the nine rows after it waited out their join budgets against a host that
			// was not hosting. The check is two state reads when the pair is live and pays the rebuild
			// in the case's setup when it is not.
			// The guarantee also undoes what the previous row declared it leaves, before it judges the
			// world live: a full bag fails the free-slot probe and used to pay a world rebuild
			// (measured 2026-11-25: overlay-sound cost 28.2 s of load after give-item filled all six
			// slots). A row that changes the run has its leftovers undone with the run, so it declares none.
			context.Notes.Add(await caseSession.EnsureLiveWorldAsync(
				CancellationToken.None,
				runChangedByTheLastRow ? CaseLeftovers.None : leftByTheLastRow,
				previousCase,
				rulesBaseline).ConfigureAwait(false));
			// A fresh run is paid only when the last row changed the run (it left the world or continued a save): the
			// next row would otherwise start inside whatever that row left behind. A row does NOT get a fresh run just
			// because it wants a clean world — one world build is 25–35 s, and giving sixty rows their own world turned a
			// nine-minute batch into forty (measured 2026-11-25: ~2000 s of setup against ~500 s of case work). Rows that
			// need a pristine detail get it from the freshness probes inside EnsureLiveWorldAsync (breakable ground, a free
			// slot, a single member, the bodies' own walls), which rebuild only that detail.
			if (runChangedByTheLastRow)
			{
				context.Notes.Add(await caseSession.StartFreshRunAsync(CancellationToken.None).ConfigureAwait(false));
			}

			// The row is pinned to the run it was given. The game itself can leave the world mid-row — its own
			// PlayerCamera.ToMainMenu is intercepted into a mid-run cut and a menu return — and the row then reads the
			// replacement: measured 2026-11-25, `drop-mid-transfer` read the host's bag while the new run was still
			// placing the character's starting supplies, and reported the give's own slot missing. The pin is taken
			// here, before the panels close, because that leave came 2.7 s after the world guarantee.
			var runUnderTheRow = runChangedByTheLastRow
				? string.Empty
				: await caseSession.ReadRunIdentityAsync(CancellationToken.None).ConfigureAwait(false);

			// The member's transport id is not stable across a rejoin, and every probe names the member by it: take the
			// id the host's roster shows NOW, or the row opens a view for a player the host no longer has.
			if (await caseSession.SingleMemberIdAsync(CancellationToken.None).ConfigureAwait(false) is { Length: > 0 } liveMember)
			{
				context.UseMember(liveMember);
			}

			// Our window holds the game's own pointer and keys: a row that drives either one reads a
			// failure that belongs to the row before it. The runner takes it down for every row, the same
			// way it guarantees the world (measured 2026-11-22: the bandage roll never turned while the
			// window was up, and the batch's failing set moved between two runs of one build).
			context.Notes.Add(await caseSession.CloseOurPanelsAsync(CancellationToken.None).ConfigureAwait(false));

			// The log mark is the case's own boundary: every warning/error after it is caused by this
			// case. A mark that cannot be read is a failed acceptance, not a zero count. The host and
			// the member are separate processes, so both marks are requested before either is awaited:
			// the two file reads must not be paid one after the other.
			var hostMarkTask = context.Session.LogOffsetAsync(options.HostUrl, CancellationToken.None);
			var memberMarkTask = context.Session.LogOffsetAsync(options.MemberUrl, CancellationToken.None);
			try
			{
				hostMark = await hostMarkTask.ConfigureAwait(false);
			}
			catch (Exception exception) when (exception is DriverException or IOException or InvalidOperationException)
			{
				markProblem = $"host log could not be marked: {exception.Message}";
			}

			try
			{
				memberMark = await memberMarkTask.ConfigureAwait(false);
			}
			catch (Exception exception) when (exception is DriverException or IOException or InvalidOperationException)
			{
				// A host-only case may legitimately have no member attached; the missing member count
				// is reported, never hidden, and the host count is still gated.
				memberMark = -1L;
				markProblem ??= $"member log could not be marked: {exception.Message}";
			}

			run.Start();
			// A case gets a hard budget of its own. Every wait inside a row has one, but a row can still leave the run
			// parked — an await that never comes back costs the whole batch its report (measured 2026-11-22, `every-17`:
			// `medical-bandage` started at 07:00:23 and the batch was still sitting there at 07:15 with both clients
			// answering and no case line written). The runner reports the timeout as the row's failure instead of
			// stalling the queue.
			var caseBudget = TimeSpan.FromSeconds(240);
			var caseWork = acceptanceCase.RunAsync(context, CancellationToken.None);
			if (!ReferenceEquals(await Task.WhenAny(caseWork, Task.Delay(caseBudget)).ConfigureAwait(false), caseWork))
			{
				throw new TimeoutException($"the case did not finish within {caseBudget.TotalSeconds:F0} s and was abandoned");
			}

			await caseWork.ConfigureAwait(false);

			// The same pin, read again: a row that did not declare a run change must have measured the run it was
			// given. A run-changing row is exempt by contract (it continues a save, restarts the host or ends the run).
			if (runUnderTheRow.Length > 0
				&& ulong.TryParse(runUnderTheRow, NumberStyles.None, CultureInfo.InvariantCulture, out var pinnedRun)
				&& pinnedRun != 0)
			{
				var runAfterTheCase = await caseSession.ReadRunIdentityAsync(CancellationToken.None).ConfigureAwait(false);
				if (!string.Equals(runUnderTheRow, runAfterTheCase, StringComparison.Ordinal))
				{
					throw new InvalidOperationException(
						"the host started run " + runAfterTheCase + " under this row (it was given run " + runUnderTheRow
						+ "), so what it measured is not the run it was given");
				}
			}

			run.Stop();
			var setup = context.Session.SetupMilliseconds;
			var caseMs = Math.Max(0, run.Elapsed.TotalMilliseconds - setup);
			var (hostLogs, memberLogs, logProblem) = await ReadLogsAsync(context.Session, options, hostMark, memberMark, markProblem).ConfigureAwait(false);
			var logFailure = JudgeLogs(acceptanceCase, hostLogs, memberLogs, logProblem);
			timeline.End(acceptanceCase.Name, logFailure is null ? "ok" : "fail");
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
				memberLogs,
				startedUtc,
				DateTimeOffset.UtcNow);
		}
		catch (Exception exception) when (exception is not OperationCanceledException)
		{
			connect.Stop();
			run.Stop();
			var setup = context?.Session.SetupMilliseconds ?? 0;
			var caseMs = Math.Max(0, run.Elapsed.TotalMilliseconds - setup);
			var (hostLogs, memberLogs, logProblem) = context is null
				? (null, null, (string?)null)
				: await ReadLogsAsync(context.Session, options, hostMark, memberMark, markProblem).ConfigureAwait(false);
			var detail = exception is DriverException or AssertionException or TimeoutException
				? exception.Message
				: exception.GetType().Name + ": " + exception.Message;
			var note = detail + (logProblem is null ? string.Empty : $" ({logProblem})");
			timeline.End(acceptanceCase.Name, "fail");
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
				memberLogs,
				startedUtc,
				DateTimeOffset.UtcNow);
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
		// The two counts are independent reads in two processes: ask both before awaiting either.
		var hostLogsTask = hostMark >= 0 ? session.LogSinceAsync(options.HostUrl, hostMark, CancellationToken.None) : null;
		var memberLogsTask = memberMark >= 0 ? session.LogSinceAsync(options.MemberUrl, memberMark, CancellationToken.None) : null;
		if (hostLogsTask is not null)
		{
			try
			{
				hostLogs = await hostLogsTask.ConfigureAwait(false);
			}
			catch (Exception exception) when (exception is DriverException or IOException or InvalidOperationException)
			{
				problem ??= $"host log count failed: {exception.Message}";
			}
		}

		if (memberLogsTask is not null)
		{
			try
			{
				memberLogs = await memberLogsTask.ConfigureAwait(false);
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
		var hostMarkTask = session.LogOffsetAsync(options.HostUrl, CancellationToken.None);
		var memberMarkTask = session.LogOffsetAsync(options.MemberUrl, CancellationToken.None);
		try
		{
			hostMark = await hostMarkTask.ConfigureAwait(false);
		}
		catch (Exception exception) when (exception is DriverException or IOException or InvalidOperationException)
		{
			problem = $"the suite could not mark the host log: {exception.Message}";
		}

		try
		{
			memberMark = await memberMarkTask.ConfigureAwait(false);
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
		var hostTotalTask = start.Host >= 0 ? session.LogSinceAsync(options.HostUrl, start.Host, CancellationToken.None) : null;
		var memberTotalTask = start.Member >= 0 ? session.LogSinceAsync(options.MemberUrl, start.Member, CancellationToken.None) : null;
		if (hostTotalTask is not null)
		{
			try
			{
				hostTotal = await hostTotalTask.ConfigureAwait(false);
			}
			catch (Exception exception) when (exception is DriverException or IOException or InvalidOperationException)
			{
				problem ??= $"the suite's host log count failed: {exception.Message}";
			}
		}

		if (memberTotalTask is not null)
		{
			try
			{
				memberTotal = await memberTotalTask.ConfigureAwait(false);
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

internal sealed class CaseContext(Options options, string outputDirectory, DriverClient host, SessionDriver session, Timeline timeline) : IAsyncDisposable
{
	private readonly Options _options = options;
	private readonly Timeline _timeline = timeline;
	private DriverClient? _member;
	private string? _memberId = options.MemberId;

	public DriverClient Host { get; } = host;
	public SessionDriver Session { get; } = session;

	public string OutputDirectory { get; } = outputDirectory;

	public string? SaveRoot => _options.SaveRoot;

	public string? PidsPath => _options.PidsPath;

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

	/// <summary>
	/// The host's dated log (<c>latest.log</c>), which — unlike <see cref="HostLogPath"/> — carries a
	/// <c>[yyyy-MM-dd HH:mm:ss.fff]</c> stamp on every line. A row that has to prove a line was written
	/// after its own gesture needs that stamp, because the undated file cannot tell a fresh line from one a
	/// previous run left behind (measured 2026-11-25: the refusal was on disk and the row could not date it).
	/// </summary>
	public string HostDatedLogPath =>
		_options.GameDir is { } datedGameDir
			? Path.Combine(datedGameDir, "BepInEx", "logs", "latest.log")
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

			await Task.Delay(TimeSpan.FromMilliseconds(250), cancellationToken).ConfigureAwait(false);
		}

		throw new InvalidOperationException("the member's own body did not spawn");
	}

	/// <summary>
	/// Gives the member a whole body by starting a fresh run, then re-reads who the member is and waits
	/// for the new body. A severed limb never heals back, so a row that needs one the batch has already
	/// taken cannot be repaired on the maimed body: it asks for a new character instead of failing
	/// (measured 2026-11-22, `every-16`: `medical-tourniquet` spent 57.1 s on fallbacks and then failed
	/// with "no limb of the member's body has a lower-limb chain and can take a tourniquet").
	/// </summary>
	public async Task StartFreshBodyAsync(CancellationToken cancellationToken)
	{
		Note("starting a fresh run so the member has a whole body");
		await Session.StartFreshRunAsync(cancellationToken).ConfigureAwait(false);
		if (await Session.SingleMemberIdAsync(cancellationToken).ConfigureAwait(false) is { Length: > 0 } member)
		{
			UseMember(member);
		}

		await WaitForMemberBodyAsync(cancellationToken).ConfigureAwait(false);
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
		while (deadline.Elapsed < TimeSpan.FromSeconds(1))
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

		Note("the custody projection reported a mismatch and no clean transition within 1 s; continuing with the case's own assertions");
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
		_timeline.Step($"{key} = {Readings[key]}");
	}

	public void Note(string text)
	{
		Notes.Add(text);
		_timeline.Step(text);
	}

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
		var read = await RecipeAsync("block-find-breakable", cancellationToken, ("count", "1"), ("minOffset", "1"), ("radius", "16")).ConfigureAwait(false);
		if (read is not { ValueKind: JsonValueKind.Object } root
			|| !root.TryGetProperty("cells", out var cells)
			|| cells.ValueKind != JsonValueKind.Array
			|| cells.GetArrayLength() == 0)
		{
			Read("scan " + startX + "," + startY, read);
			return null;
		}

		var cell = cells[0];
		if (!cell.TryGetProperty("x", out var xValue) || xValue.ValueKind != JsonValueKind.Number
			|| !cell.TryGetProperty("y", out var yValue) || yValue.ValueKind != JsonValueKind.Number
			|| !cell.TryGetProperty("block", out var blockValue) || blockValue.ValueKind != JsonValueKind.Number)
		{
			Read("scan " + startX + "," + startY, read);
			return null;
		}

		var x = (int)xValue.GetDouble();
		var y = (int)yValue.GetDouble();
		var block = (int)blockValue.GetDouble();
		Read("scan " + x + "," + y, read);
		return (x, y, block);
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
	/// True when the row leaves the run itself changed — it continued a save, restarted the host, ended
	/// the run or descended a layer — so the world the next row finds is not the one it would start in.
	/// The runner starts a fresh run before that next row (measured 2026-11-24: eleven rows that pass
	/// alone failed in one batch, and the rows before them had continued or killed the run).
	/// </summary>
	public virtual bool LeavesTheRunChanged => RunChangingRows.Contains(Name);

	/// <summary>
	/// The rows that continue a save, restart the host, end the run or descend a layer. Kept by name in
	/// one place so the list is auditable; a row overrides <see cref="LeavesTheRunChanged"/> when it
	/// changes the run for a different reason.
	/// </summary>
	private static readonly HashSet<string> RunChangingRows = new(StringComparer.Ordinal)
	{
		"save-continue",
		"save-write-interrupted",
		"save-continue-reentry",
		"player-deaths",
		"host-after-continue",
		"host-crash-restart",
		"two-member-continue",
		"run-end",
		"last-layer-gate",
		"layer-panel",
	};

	/// <summary>
	/// What the case costs the suite. <see cref="CaseCost.InRun"/> cases verify inside one world and
	/// restore their own mutation; <see cref="CaseCost.NewWorld"/> cases need a fresh run or end the
	/// world; <see cref="CaseCost.ClientRestart"/> cases need a process (or a first-world character).
	/// The report groups by this value so a fast regression run can select only the cheap group.
	/// </summary>
	public virtual CaseCost Cost => CaseCost.InRun;

	/// <summary>
	/// What this case leaves for the next row (see <see cref="CaseLeftovers"/>). Declare a fact when the row changes it
	/// and does not put it back on every path — including the failure paths, where a row's own restore is skipped.
	/// Over-declaring costs one game verb and one probe in the next row's setup; under-declaring hands the next row a
	/// world the row before it made, which is how rows that pass alone failed in the batch (measured 2026-11-25).
	/// </summary>
	public virtual CaseLeftovers Leaves => CaseLeftovers.None;


	// (No "runs in the batch" switch: acceptance is one run over every registered case — a case that
	// costs a world restart says so in Cost, and the runner pays it there instead of hiding the case.)

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

/// <summary>
/// What a case leaves behind in the shared run, one flag per fact the next row would otherwise start inside.
/// </summary>
/// <remarks>
/// The batch runs every row in one world (<c>docs/acceptance/AGENTS.md</c> rule 10): a fresh run is paid only when
/// the previous case left or changed the run. Every other fact a row changes has to be undone between rows, and the
/// row that changed it is the only one that knows what it changed — hence a declaration, not a sweep the runner
/// guesses at. The runner undoes a declared fact through the game's own entry point (the <c>/heal</c> verb,
/// <c>Body.DropItem</c>, <c>/gamerule</c>, <c>Time.timeScale</c>, <c>PauseHandler.isPaused</c>, the body's own
/// transform) and then proves with a probe that the undo took, before the next row's own body runs.
/// Panels are not declared: the runner closes our window for every row
/// (<c>SessionDriver.CloseOurPanelsAsync</c>), because the window holds the game's own pointer and keys.
/// </remarks>
[Flags]
internal enum CaseLeftovers
{
	/// <summary>The row hands the next one the world as it found it.</summary>
	None = 0,

	/// <summary>Injury, bleeding, a seeded wound, pain or a missing limb on either body.</summary>
	Body = 1,

	/// <summary>Items in either body's carried set (the bag).</summary>
	Bag = 2,

	/// <summary>An item left in either body's hand.</summary>
	Hands = 4,

	/// <summary>A body left away from the host's side, so a gesture that needs the pair together reads nothing.</summary>
	Position = 8,

	/// <summary>A <c>/gamerule</c> switch the run did not start with.</summary>
	Rules = 16,

	/// <summary>A time scale or a pause the row set, which every later wait then measures against.</summary>
	Clock = 32,
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
	LogCounts? MemberLogs,
	DateTimeOffset StartedUtc,
	DateTimeOffset FinishedUtc)
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
	public static string Render(Options options, IReadOnlyList<CaseResult> results, double sharedSetupMs, double totalMs, SuiteLogs suiteLogs, DateTimeOffset startedUtc, DateTimeOffset finishedUtc, DateTimeOffset pairStartedUtc, DateTimeOffset pairReadyUtc, Timeline timeline)
	{
		var builder = new StringBuilder();
		builder.AppendLine("# Acceptance suite report");
		builder.AppendLine();
		builder.AppendLine($"- Host: `{options.HostUrl}`");
		builder.AppendLine($"- Start (UTC): `{Timestamp(startedUtc)}`");
		builder.AppendLine($"- Pair start (UTC): `{Timestamp(pairStartedUtc)}`");
		builder.AppendLine($"- Pair ready (UTC): `{Timestamp(pairReadyUtc)}`");
		builder.AppendLine($"- Finish (UTC): `{Timestamp(finishedUtc)}`");
		builder.AppendLine($"- Wall time: {Seconds(totalMs)} s (shared pair setup {Seconds(sharedSetupMs)} s)");
		var metadata = BuildMetadata.Read(options.GameDir);
		builder.AppendLine($"- Build: game `{metadata.GameBuild ?? "unknown"}`, mod `{metadata.ModBuild ?? "unknown"}`, artifact `{metadata.Artifact ?? "unknown"}`");
		var notRun = CaseRegistry.All
			.Where(candidate => !results.Any(result => string.Equals(result.Name, candidate.Name, StringComparison.Ordinal)))
			.Select(candidate => candidate.Name)
			.ToList();
		builder.AppendLine(notRun.Count == 0
			? "- Cases: every registered case ran."
			: $"- Cases NOT run by this selection: {string.Join(", ", notRun)}.");
		builder.AppendLine();
		builder.Append(timeline.Render());
		builder.AppendLine($"- Logs outside cases: host {Counts(suiteLogs.HostOutside)}, member {Counts(suiteLogs.MemberOutside)} — {(suiteLogs.OutsideErrors == 0 && suiteLogs.Problem is null ? "PASS" : "FAIL")}");
		builder.AppendLine();
		builder.AppendLine("| Case | Cost | Start (UTC) | End (UTC) | Connect | Setup | Case | Total | Host W/E | Member W/E | Verdict | Note |");
		builder.AppendLine("|---|---|---|---|---|---|---|---|---|---|---|---|");
		foreach (var result in results)
		{
			builder.AppendLine($"| {result.Name} | {result.Cost} | {Timestamp(result.StartedUtc)} | {Timestamp(result.FinishedUtc)} | {Seconds(result.ConnectMs)} | {Seconds(result.SetupMs)} | {Seconds(result.CaseMs)} | {Seconds(result.TotalMs)} | {Counts(result.HostLogs)} | {Counts(result.MemberLogs)} | {(result.Passed ? "PASS" : "FAIL")} | {Escape(result.Note)} |");
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
			builder.AppendLine($"- start `{Timestamp(result.StartedUtc)}`, end `{Timestamp(result.FinishedUtc)}`");
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

	public static async Task WriteJsonAsync(string path, Options options, IReadOnlyList<CaseResult> results, double sharedSetupMs, double totalMs, SuiteLogs suiteLogs, DateTimeOffset startedUtc, DateTimeOffset finishedUtc, DateTimeOffset pairStartedUtc, DateTimeOffset pairReadyUtc, Timeline timeline)
	{
		var metadata = BuildMetadata.Read(options.GameDir);
		var payload = new
		{
			suite = options.Suite,
			host = options.HostUrl,
			build = new
			{
				game = metadata.GameBuild,
				mod = metadata.ModBuild,
				artifact = metadata.Artifact,
			},
			startedAtUtc = Timestamp(startedUtc),
			pairStartedAtUtc = Timestamp(pairStartedUtc),
			pairReadyAtUtc = Timestamp(pairReadyUtc),
			finishedAtUtc = Timestamp(finishedUtc),
			totalMs,
			timeline = timeline.Lines,
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
				startedAtUtc = Timestamp(result.StartedUtc),
				finishedAtUtc = Timestamp(result.FinishedUtc),
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

	private static string Timestamp(DateTimeOffset value) => value.UtcDateTime.ToString("yyyy-MM-dd'T'HH:mm:ss.fff'Z'", CultureInfo.InvariantCulture);

	private static string Escape(string text) => text.Replace("|", "\\|", StringComparison.Ordinal).ReplaceLineEndings(" ");
}

internal sealed record Options
{
	public bool List { get; init; }

	public string Suite { get; init; } = "all";

	public string? Cost { get; init; }

	public bool Reuse { get; init; }

	public bool Fresh { get; init; }

	/// <summary>
	/// Join the member while the host generates its world, so the two loads overlap. On 2026-11-15 this also
	/// removed the visible lobby wait: the member used to sit in the lobby for the host's whole generation
	/// (23 s) before its own load began. Parallel pairing measured 40.9 s from endpoints to a ready pair,
	/// serial 47.8 s. `--serial-setup` restores the serial order for a case that needs it.
	/// </summary>
	public bool ParallelSetup { get; init; } = true;

	public string HostUrl { get; init; } = "ws://127.0.0.1:18590";

	public string MemberUrl { get; init; } = "ws://127.0.0.1:18591";

	/// <summary>
	/// The third client (Steam2). Only up when the launch asked for it (`launch --all`); a row that needs
	/// two members reports this endpoint as missing rather than skipping itself.
	/// </summary>
	public string AltUrl { get; init; } = "ws://127.0.0.1:18592";

	public string? OutputDirectory { get; init; }

	public string? GameDir { get; init; }

	/// <summary>The scratch save root the launcher gave the clients; a case may corrupt only this archive.</summary>
	public string? SaveRoot { get; init; }

	/// <summary>The launcher's pids snapshot; a case that kills the host reads its pid from here.</summary>
	public string? PidsPath { get; init; }

	/// <summary>
	/// The clients' exit codes, held from the moment the suite starts driving them. Set once by the
	/// run, after the launcher has written its snapshot; a refused socket is reported with the code.
	/// </summary>
	public ClientExitLedger? ClientExits { get; init; }

	/// <summary>
	/// The eval's own timeout inside the action budget. The driver's eval must finish before the action's
	/// cancellation fires, or a slow snippet reports as a missing answer; the one-off drive command
	/// therefore passes a value derived from its budget.
	/// </summary>
	public int DriverEvalTimeoutMs { get; init; } = 60_000;

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
				case "--parallel-setup":
					options = options with { ParallelSetup = true };
					break;
				case "--serial-setup":
					options = options with { ParallelSetup = false };
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
				case "--save-root" when index + 1 < args.Length:
					options = options with { SaveRoot = args[++index] };
					break;
				case "--pids" when index + 1 < args.Length:
					options = options with { PidsPath = args[++index] };
					break;
				case "--member" when index + 1 < args.Length:
					options = options with { MemberId = args[++index] };
					break;
				case "--alt" when index + 1 < args.Length:
					options = options with { AltUrl = args[++index] };
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
		// Un-parked 2026-11-18: the case restarts the run itself, so the first world entry grants the kit
		// (the 2026-11-12 park needed a first-entry character that the shared run could not give it).
		new StartingSuppliesCase(),
		// The three rows that read state the rest of the run consumes: the character's unspent starting
		// materials, a lock the run has not picked yet, and the boot log's own capability account. They
		// sit right after the restart so a single pass still finds that state.
		new CraftingCase(),
		new LockpickCase(),
		// `capability-report` is not a row: the adapter's report is a boot log, and its text, verdict and refusal
		// rule are covered by `AdapterCapabilityReportTests` in the unit suite. A case that reads our own log with no
		// player action proves nothing a gate does not (ticket: 1.0-rebuild-each-acceptance-row-from-the-game).
		new MedicalChartCase(),
		// Re-registered 2026-11-18 for the ticket's next step: the row now records `minigame-live` at the
		// first press, so its failure names the minigame's own state (holding, handStarted, angle, fill) at
		// the moment the staged press lands. The item's minigame advances over frames, so the row takes
		// as long as the game needs and declares no restore of its own.
		new MedicalBandageCase(),
		new ItemRestingsCase(),
		new ItemCustomizationCase(),
		new GiveItemCase(),
		new OverlaySoundCase(),
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
		new WorldCensusCase(),
		// Un-parked 2026-11-18: the 2026-11-13 park was the recipe's own product sprite throwing inside
		// RefreshRecipeList (the crafting-screen ticket, fixed 2026-11-14 with a placeholder prefab).
		new CraftPanelCase(),
		new ProstheticCase(),
		new RunSettingsCase(),
		new TopicalDoseCase(),
		// The world-entry fingerprint and the post-mutation one are both registered above; the notes
		// that used to sit here (2026-11-12) were the parked readings of rows now carried by
		// block-sync, block-reconcile and the two fingerprint variants.
		new ConsoleHealCase(), // un-parked 2026-11-13 01:50 with the member's own trace beside the reading; re-parked 2026-11-13 01:30: the full-suite run read skinHealth 0.334, pain 83.36,
		// dislocated true after the host's /heal @a answered applied:true — the reset lands and the seeded wound's own
		// tick re-damages before the read. The earlier 17/17 pass was a single race, not a fix. Reading on the suite ticket.
		// un-parked 2026-11-13: the suite owns the member now (the stale-session
		// reading of 2026-11-12 judged a member the host's world never adopted); the row's judge is
		// the member's own limb after the host's `/heal @a`.
		// own limb stays partial in an aged session; the reading is on the suite ticket.
		new EatSoundCase(), // a host's own eat plays on the member's machine
		// The save/continue row takes its cut after a real trap blast (the blast is staged inside the
		// case): the 2026-11-15 merged run showed that a world damaged before the save is where the
		// restore's world-entity rows go missing, so the reading belongs to the row that saves.
		new SaveContinueCase(),
		// The worlds page lists archive rows; it needs a run that has actually produced a cut, so it
		// follows save-continue instead of reading an empty fresh save root.
		new WorldsPageCase(),
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
		new RoleClearingCase(), // the host ends its run: the member drops its role and rejoins a new session without a restart
		// Continue first, then host: the session must open over the running world. It gives up the session
		// and restores a world, and that restore is the cost it declares.
		new HostAfterContinueCase(),
		new RemoteRemovalCase(), // the member leaves last; the host's displayed body goes and its own log says so
		new RunEndCase(), // the host leaves the world: the member leaves it too (run-end broadcast)
		// The session picker unlocks all three portraits; the host starts this shared world with the
		// game's own unchipped preset and leaves it running for the late-layer rows below.
		new CharacterChoiceCase(),
		new LateLayerSwitchesCase(), // both clients derive the host's switches in the shared world
		// The drill pod is the native two-layer trigger: it waits for every living member to stand at
		// the pod before the host's descent, and refuses (with the missing names) when one is away.
		new PodTeamWaitCase(),
		// The last layer's own gate: both clients generate layer 10 of an unchipped run,
		// the cutscene clears each client's local blockItemUsage, and each client's own
		// staged attack moves its own native attack state.
		new LastLayerGateCase(),
		// Last row in the shared world: the Merc ending must not write either solo unlock key, and
		// the host's native solo picker must come back after the session is left.
		new CharacterSoloLockCase(),
		// The per-player death count (the user's decision 2026-11-14): two deaths with a revive between
		// them, read from both clients, then the same count after a save and a continue; that restore is
		// the cost it declares.
		new PlayerDeathsCase(),
		// The body-action switches (the user's 2026-11-14 decision): a refused push named on the pusher's
		// own client with the target unmoved, and a carry the game's rule refuses let through by the host's
		// allow_always_carry. It writes host rules, so it is registered with the pair-shaping rows at the
		// end of the list, and it restores them before it ends.
		// Batch 1's tourniquet row: the game's own lower-limb chain stops bleeding and nothing else
		// does. It starts its own world to have a body worth treating.
		new MedicalTourniquetCase(),
		// Batch 1's splint row: a splint on a fractured limb sets the game's own splinted flag and is
		// consumed, and the wound view's special action takes it off and hands the item back.
		new MedicalSplintCase(),
		// Batch 1's injection row: the doctor's own release starts a medical operation, the syringe's
		// increments commit, and the member's own client runs the game's native Inject (fentanyl's
		// Painkillers effect is the readable value).
		new MedicalInjectionCase(),
			// The CPR controls read at the end: the operator's panel, both channels over a real
			// operation, and the patient's own restart and oxygen. A run of presses costs a few seconds
			// of real time, which is the cost it declares.
			new MedicalCprCase(),
			// The shrapnel session: the operator's special starts the host-authoritative operation and
			// the patient's own limb loses the piece. The row read its limb with the game's CLEARING call
			// (seed-shrapnel count 0) until 2026-11-18, so its "remaining" was its own erase; it now reads
			// through limb-read and the piece count is a fact.
			new MedicalShrapnelCase(),
			// The block-placement sound: the host places metal scrap through the game's own use path and the
			// member's recorder must carry the scrapmetal clip. The audible half of the 2026-09-06 report had
			// no device until 2026-11-19, so this is the row that reads it.
			new BlockPlaceSoundCase(),
			// The block-break sound: the host rolls the game's own damage until a block is gone and the
			// member's recorder must carry that block kind's own hit clip. The audible half of the 2026-09-21
			// report had no device until 2026-11-19.
			new BlockBreakSoundCase(),
			// The give's container half: an item given into the member's own trash bag must play the
			// game's backpack clip on the client whose container the item enters. A body-slot give is
			// silent natively, so this row stages the container case only; its ticket is
			// `1.0-give-container-sound-unread`.
			new GiveContainerSoundCase(),
			// The crafting outcome: a real craft through the game's own `TryMake` entry consumes the material
			// and lands the product in the crafter's bag, and the host's view of that player renders it. The
			// panel row only opens the panel; this is the row that reads the outcome.
			new CraftingOutcomeCase(),
			// The two-member save/continue read 1 character in the cut on 2026-11-19. The root cause was
			// the name-only IP-direct key: two clients presenting the same display name shared a key and
			// `WorldCharacterBinder.Collect` refused both rows. Each client now carries a ClientIdentity
			// (decision 272), and this row is the reading that proves the shared name keeps two characters.
			new TwoMemberContinueCase(),
			// The real drag: a bandage picked up by the game's own drag gesture and released onto the wound
			// view's limb rect starts the game's bandage minigame — the hand path, not the direct use call.
			new RealDragCase(),
		// The mid-run late join (2026-11-19): a host already in a running world, a member joining after.
		new LateJoinCase(),
		// The sample mod's content inside a session (2026-11-19): its item, registry entry and limb action.
		new SampleModCase(),
		// The failure-path rows (2026-11-20, the acceptance-expansion ticket): these stage failure states.
		new DropMidTransferCase(),
		new SaveWriteInterruptedCase(),
		new SaveContinueReentryCase(),
		new RemoteBackpackRefreshCase(),
		// The three rows that shape the pair are last, in this order: a lossy profile would change every
		// later reading, the rule switches write host rules, and the crash row kills the host process.
		new NetConditionsCase(),
		new HostRuleTogglesCase(),
		new HostCrashRestartCase(),
		// Ending (2026-11-18 reading): the host's own tracker records ending=Merc when the game's
		// MercEnding runs, and the member's tracker stays ending=None|authority=False eight seconds
		// later — the kind rides the run row, which advances at the layer end that a fresh by-name
		// session does not reach. The old declaration path also no longer registers a type on the
		// deployed evaluator, so the fixture has two halves to rebuild before the row can be read.
		// new EndingCase(),
	];
}

internal sealed class AssertionException(string message) : Exception(message);













