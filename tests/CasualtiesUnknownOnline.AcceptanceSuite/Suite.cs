using System.Diagnostics;
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
		if (options.Problem is { } problem)
		{
			Console.Error.WriteLine(problem);
			Console.Error.WriteLine("usage: CasualtiesUnknownOnline.AcceptanceSuite [--list | --suite <name|all>] [--out <dir>] [--host <ws-url>] [--game-dir <dir>] [--member <id>]");
			return 64;
		}

		if (options.List)
		{
			foreach (var acceptanceCase in CaseRegistry.All)
			{
				Console.WriteLine($"{acceptanceCase.Name}\t{acceptanceCase.Launch}");
			}

			return 0;
		}

		var selected = options.Suite == "all"
			? CaseRegistry.All
			: [.. CaseRegistry.All.Where(candidate => string.Equals(candidate.Name, options.Suite, StringComparison.Ordinal))];
		if (selected.Count == 0)
		{
			Console.Error.WriteLine($"no case named '{options.Suite}'");
			return 64;
		}

		var outputDirectory = Path.GetFullPath(options.OutputDirectory ?? Path.Combine(RepositoryPaths.Root, ".acceptance", "suite"));
		Directory.CreateDirectory(outputDirectory);
		var results = new List<CaseResult>();
		var session = new SessionDriver(options);
		var sessionNote = await session.EnsureInWorldAsync(CancellationToken.None).ConfigureAwait(false);
		var memberNote = await session.EnsureMemberAsync(CancellationToken.None).ConfigureAwait(false);
		// The host names its members only once the member's own scene report has landed, which can
		// trail the join by a beat: poll briefly instead of reading once and losing the id.
		string[] members = [];
		for (var wait = 0; wait < 30; wait++)
		{
			members = await session.ReadMembersAsync(CancellationToken.None).ConfigureAwait(false);
			if (members.Length > 0)
			{
				break;
			}

			await Task.Delay(TimeSpan.FromMilliseconds(500), CancellationToken.None).ConfigureAwait(false);
		}

		if (members.Length > 0)
		{
			options = options with { MemberId = members[0] };
		}

		Console.WriteLine($"session\t{sessionNote}");
		Console.WriteLine($"member\t{memberNote}");
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
			Console.WriteLine($"{acceptanceCase.Name}\t{(results[^1].Passed ? "PASS" : "FAIL")}\t{results[^1].Note}");
		}

		var reportPath = Path.Combine(outputDirectory, "report.md");
		await File.WriteAllTextAsync(reportPath, SuiteReport.Render(options, results)).ConfigureAwait(false);
		Console.WriteLine($"report: {reportPath}");
		return results.All(result => result.Passed) ? 0 : 1;
	}

	private static async Task<CaseResult> RunAsync(AcceptanceCase acceptanceCase, Options options, string outputDirectory)
	{
		await using var host = new DriverClient(options.HostUrl);
		CaseContext? context = null;
		try
		{
			await host.ConnectAsync(CancellationToken.None).ConfigureAwait(false);
			context = new CaseContext(options, outputDirectory, host, new SessionDriver(options));
			await acceptanceCase.RunAsync(context, CancellationToken.None).ConfigureAwait(false);
			return new CaseResult(acceptanceCase.Name, acceptanceCase.Launch, true, "ok", context.Readings, context.Notes);
		}
		catch (Exception exception) when (exception is DriverException or AssertionException or IOException or InvalidOperationException)
		{
			return new CaseResult(
				acceptanceCase.Name,
				acceptanceCase.Launch,
				false,
				exception.Message,
				context?.Readings ?? new Dictionary<string, string>(StringComparer.Ordinal),
				context?.Notes ?? []);
		}
		finally
		{
			if (context is not null)
			{
				await context.DisposeAsync().ConfigureAwait(false);
			}
		}
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
	public async Task ClearBodyAsync(CancellationToken cancellationToken)
	{
		var bag = await RecipeAsync("body-items", cancellationToken).ConfigureAwait(false);
		Read("body-items", bag);
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

	public abstract Task RunAsync(CaseContext context, CancellationToken cancellationToken);
}

internal enum LaunchKind
{
	Warm,
	Fresh,
	Restart,
}

internal sealed record CaseResult(string Name, LaunchKind Launch, bool Passed, string Note, IReadOnlyDictionary<string, string> Readings, IReadOnlyList<string> Notes);

internal static class SuiteReport
{
	public static string Render(Options options, IReadOnlyList<CaseResult> results)
	{
		var builder = new StringBuilder();
		builder.AppendLine("# Acceptance suite report");
		builder.AppendLine();
		builder.AppendLine($"- Host: `{options.HostUrl}`");
		builder.AppendLine($"- Started: {DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss zzz}");
		builder.AppendLine();
		builder.AppendLine("| Case | Launch | Verdict | Note |");
		builder.AppendLine("|---|---|---|---|");
		foreach (var result in results)
		{
			builder.AppendLine($"| {result.Name} | {result.Launch} | {(result.Passed ? "PASS" : "FAIL")} | {Escape(result.Note)} |");
		}

		builder.AppendLine();
		builder.AppendLine($"**{results.Count(result => result.Passed)} passed, {results.Count(result => !result.Passed)} failed.**");
		foreach (var result in results)
		{
			builder.AppendLine();
			builder.AppendLine($"## {result.Name} — {(result.Passed ? "PASS" : "FAIL")}");
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

	private static string Escape(string text) => text.Replace("|", "\\|", StringComparison.Ordinal).ReplaceLineEndings(" ");
}

internal sealed record Options
{
	public bool List { get; init; }

	public string Suite { get; init; } = "all";

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
		// Parked (2026-11-12): the starting kit is granted when a character FIRST enters a world, and
		// this fixture reuses one long-lived session — the member's kit is long consumed by the time
		// any case runs (measured: 0 items). The row needs a character that has never entered a world,
		// which means its own session; it stays out of the all-suite run until the suite owns a
		// per-case launch. The reading is on docs/backlog/in-progress/1.0-acceptance-suite.md.
		// new StartingSuppliesCase(),
		new MedicalChartCase(),
		new ItemRestingsCase(),
		new ItemCustomizationCase(),
		new GiveItemCase(),
		new OverlaySoundCase(),
		new WorldsPageCase(),
		new TraderCase(),
		new TopicalDoseCase(),
		// new ConsoleHealCase(), // parked: the relayed reset is answered as applied while the member's
		// own limb stays partial in an aged session; the reading is on the suite ticket.
		// save-continue leaves the world and restores it: it goes after every case that only needs
		// the world the suite already owns.
		new SaveContinueCase(),
		// The ending ends the run: it goes last, after every case that needs a world.
		new EndingCase(),
		// new LockpickCase(), // parked: the hand read is named on the suite ticket; the corrected
		// aim has not been pressed on a fresh lockable yet.
	];
}

internal sealed class AssertionException(string message) : Exception(message);
