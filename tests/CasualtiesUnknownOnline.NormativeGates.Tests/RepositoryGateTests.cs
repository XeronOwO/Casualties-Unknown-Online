using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Tooling.NormativeGates;

/// <summary>
/// C# unit-test ports of the repository's data/process/absolute-path gates.
/// These cover the checks that previously lived only in PowerShell scripts.
/// </summary>
public class RepositoryGateTests
{
	private static readonly Regex DriveLetterBackslash = new(@"[A-Za-z]:\\");
	private static readonly Regex DriveLetterForwardSlash = new(@"[A-Za-z]:/[^/]");
	private static readonly Regex UnixRootPath = new(@"(^|[^:/])/(home|Users|tmp|var|opt|mnt|etc|usr|root)/");

	// The local-environment matchers of `NoLocalEnvironmentNames_InProductionCode`, kept as SHAPES rather
	// than as the values they were written for. The correction has a reason: the first version spelled the
	// values out — the proxy's address, the acceptance harness's own ports, the Sandboxie instance names —
	// and so published inside this repository exactly the facts the rule exists to keep out, while only
	// ever catching THIS machine's numbers: a gate shaped around one machine's data rather than around the
	// rule. A shape cannot reach everything the rule covers, and the limits are named rather than papered
	// over: a bare port literal in the middle of a string is indistinguishable from any other port
	// constant, and an instance name or a proxy's product name is only recognisable by the name itself —
	// a list of product names would put this machine's own back into the repository, which is the fact the
	// rule exists to keep out. Those belong to the pre-commit check
	// (`docs/en/contributing/review-and-delivery.md`), which is where the user's ruling of 2026-10-12 put
	// them. What is left is the reach a gate really has: a shipped string that tells the reader about a
	// listener or a host on the network the product was built on.
	// A listener on the machine that wrote the message: `localhost`, the loopback literal or its IPv6
	// form, followed by the port it listens on. The guard is `[^\w.]` rather than `\b`, because `\b` is
	// false between a space and `[` — the `[::1]` spelling never matched with it, which is what the
	// matcher's own pin caught.
	private static readonly Regex LoopbackEndpoint = new(@"(?:^|[^\w.])(?:localhost|127\.0\.0\.1|\[::1\]):\d{2,5}\b");

	// A private-network address is the same fact one hop out: it names the builder's own network rather
	// than the operator's. Each branch carries the octet count its own prefix implies, so all four parts
	// are required in every case and an assembly version cannot red it — the first draft shared one tail
	// between the branches and so demanded a fifth octet of the two that already carry two.
	private static readonly Regex PrivateNetworkAddress = new(@"\b(?:10(?:\.\d{1,3}){3}|192\.168(?:\.\d{1,3}){2}|172\.(?:1[6-9]|2\d|3[01])(?:\.\d{1,3}){2})\b");

	private static readonly (string What, Regex Matcher)[] LocalEnvironmentPatterns =
	[
		("a loopback endpoint carrying a port", LoopbackEndpoint),
		("a private-network address", PrivateNetworkAddress),
	];

	private static readonly Regex EntityEventKindMember = new(@"^\s*([A-Za-z_]\w*)\s*=\s*\d+,", RegexOptions.Multiline);
	private static readonly Regex EntityEventKindReference = new(@"EntityEventKind\.(\w+)");

	private const string ExpectedEventReplayHeader = "kind,sound-trigger,sound-replay,visual-trigger,visual-replay,state-consumption,status,notes";
	private static readonly string[] AllowedEventStatuses = ["covered", "excluded", "gap"];
	private static readonly string[] EventReplayMechanismColumns =
	[
		"sound-trigger",
		"sound-replay",
		"visual-trigger",
		"visual-replay",
		"state-consumption"
	];

	[Fact]
	public void NoAbsolutePaths_NoTrackedMachinePaths()
	{
		var failures = new List<string>();
		var scanned = 0;

		foreach (var file in EnumerateScannedFiles())
		{
			if (file.StartsWith("references/", StringComparison.Ordinal)
				|| file.StartsWith("references\\", StringComparison.Ordinal)
				|| Path.GetExtension(file).Equals(".dll", StringComparison.OrdinalIgnoreCase))
			{
				continue;
			}

			var fullPath = RepositoryPaths.File(file);
			if (!File.Exists(fullPath))
			{
				continue;
			}

			scanned++;
			var lines = File.ReadAllLines(fullPath);
			for (var i = 0; i < lines.Length; i++)
			{
				if (DriveLetterBackslash.IsMatch(lines[i])
					|| DriveLetterForwardSlash.IsMatch(lines[i])
					|| UnixRootPath.IsMatch(lines[i]))
				{
					failures.Add($"{file}:{i + 1}: {lines[i].Trim()}");
				}
			}
		}

		Assert.True(failures.Count == 0, $"Absolute machine paths found in {scanned} scanned files (tracked plus untracked-and-not-ignored)" + Environment.NewLine + string.Join(Environment.NewLine, failures));
	}

	[Fact]
	public void NoLocalEnvironmentNames_InProductionCode()
	{
		// The sibling of the absolute-path gate above, for the class of machine fact that is not a path: the
		// shipped product must not name the environment it happens to be built in. An operator's proxy is
		// theirs, so a message that names this author's is both useless to them and a fingerprint of the
		// machine it was written on — the user's red line, 2026-10-12, after `SteamSendFailureClassifier`
		// was found naming one in a player-facing remediation string. The matchers and what they can reach
		// are argued where they are declared.
		//
		// Scope is `src/` deliberately: tests seed fixtures and the repository's own records cite values as
		// evidence, so a scan over either would red on legitimate content and teach people to ignore it. A
		// production literal is what this rule can actually reach, and the pre-commit check covers the rest.
		var failures = new List<string>();
		var scanned = 0;

		foreach (var file in EnumerateScannedFiles().Where(file =>
			file.StartsWith("src/", StringComparison.Ordinal) || file.StartsWith("src\\", StringComparison.Ordinal)))
		{
			if (Path.GetExtension(file).Equals(".dll", StringComparison.OrdinalIgnoreCase))
			{
				continue;
			}

			var fullPath = RepositoryPaths.File(file);
			if (!File.Exists(fullPath))
			{
				continue;
			}

			scanned++;
			var lines = File.ReadAllLines(fullPath);
			for (var i = 0; i < lines.Length; i++)
			{
				foreach (var (what, matcher) in LocalEnvironmentPatterns)
				{
					if (matcher.IsMatch(lines[i]))
					{
						failures.Add($"{file}:{i + 1}: {what} — {lines[i].Trim()}");
					}
				}
			}
		}

		Assert.True(scanned > 0, "no production file was scanned; the scan surface is not src/");
		Assert.True(failures.Count == 0, $"Local environment names in production code ({scanned} src files scanned)" + Environment.NewLine + string.Join(Environment.NewLine, failures));
	}

	[Theory]
	[InlineData("if the proxy refuses the connection, retry the send", false)]
	[InlineData("a namespace clash in the mod scan", false)]
	[InlineData("an http://127.0.0.1/health probe carries no port", false)]
	[InlineData("the assembly version is 4.0.0.0", false)]
	[InlineData("connect to ws://127.0.0.1:28090 instead", true)]
	[InlineData("the evaluator listens on localhost:8080", true)]
	[InlineData("reach the other box at 192.168.1.20", true)]
	[InlineData("the host sits at 10.4.0.7", true)]
	[InlineData("the relay answered from [::1]:9000", true)]
	public void TheLocalEnvironmentMatchers_ReadShapesAndNotOrdinaryEnglish(string line, bool expected)
	{
		var matched = LocalEnvironmentPatterns.Any(pattern => pattern.Matcher.IsMatch(line));
		Assert.Equal(expected, matched);
	}

	[Fact]
	public void EventReplayMatrix_Completeness()
	{
		var path = RepositoryPaths.File("docs/contracts/event-replay-matrix.csv");
		Assert.True(File.Exists(path), "event-replay-matrix.csv missing");
		var lines = File.ReadAllLines(path);
		Assert.True(lines.Length > 0, "event-replay matrix is empty");
		Assert.True(lines[0].Trim() == ExpectedEventReplayHeader, $"event-replay matrix header mismatch\n  expected: {ExpectedEventReplayHeader}\n  actual:   {lines[0].Trim()}");

		var failures = new List<string>();
		var seen = new HashSet<string>(StringComparer.Ordinal);
		var columnCount = ExpectedEventReplayHeader.Split(',').Length;

		for (var i = 1; i < lines.Length; i++)
		{
			var line = lines[i];
			if (string.IsNullOrWhiteSpace(line))
			{
				continue;
			}

			var raw = line.Split(',');
			if (raw.Length != columnCount)
			{
				failures.Add($"line {i + 1}: {raw.Length} raw fields (expected {columnCount}) - a bare comma inside a cell breaks the CSV; reword without commas");
				continue;
			}

			var kind = raw[0];
			if (string.IsNullOrWhiteSpace(kind))
			{
				failures.Add("row with empty kind");
				continue;
			}

			if (!seen.Add(kind))
			{
				failures.Add($"duplicate kind: {kind}");
			}

			for (var c = 0; c < EventReplayMechanismColumns.Length; c++)
			{
				if (string.IsNullOrWhiteSpace(raw[c + 1]))
				{
					failures.Add($"{kind}: mechanism column '{EventReplayMechanismColumns[c]}' is empty - event not audited");
				}

				if (raw[c + 1].Contains(','))
				{
					failures.Add($"{kind}: bare comma in '{EventReplayMechanismColumns[c]}' - reword without commas");
				}
			}

			var notes = raw[7];
			if (notes.Contains(','))
			{
				failures.Add($"{kind}: bare comma in 'notes' - reword without commas");
			}

			var status = raw[6].Trim().ToLowerInvariant();
			if (!AllowedEventStatuses.Contains(status, StringComparer.Ordinal))
			{
				failures.Add($"{kind}: invalid status '{raw[6]}' (expected covered|excluded|gap)");
				continue;
			}

			if (status != "covered" && string.IsNullOrWhiteSpace(notes))
			{
				failures.Add($"{kind}: status '{status}' requires a notes entry (why / owning domain)");
			}
		}

		// The matrix documents the kind set the runtime declares — a row per EntityEventKind and no row for
		// a kind the enum does not carry. This is the census the per-row rules cannot give: a table reduced
		// to its header satisfies every one of them by having no rows left to fail on.
		failures.AddRange(MatrixKindFailures(EntityEventKinds(), seen));

		Assert.True(failures.Count == 0, "Event-replay gate failed" + Environment.NewLine + string.Join(Environment.NewLine, failures));
	}

	[Fact]
	public void TheMatrixKindCensus_FlagsAMissingRowAndARowForAKindTheEnumDoesNotDeclare()
	{
		var failures = MatrixKindFailures(["MineExploded", "TurretFired"], ["MineExploded", "RetiredKind"]);
		Assert.Contains(failures, failure => failure.Contains("TurretFired", StringComparison.Ordinal));
		Assert.Contains(failures, failure => failure.Contains("RetiredKind", StringComparison.Ordinal));
		Assert.Empty(MatrixKindFailures(["MineExploded"], ["MineExploded"]));
	}

	/// <summary>The runtime's entity-event kinds, parsed from the enum that declares them — the source of
	/// truth the matrix census and the dispatch census both read.</summary>
	private static HashSet<string> EntityEventKinds()
	{
		var enumFile = RepositoryPaths.File("src/CasualtiesUnknownOnline.Runtime/Protocol/EntityEventKind.cs");
		Assert.True(File.Exists(enumFile), "EntityEventKind.cs missing");
		return EntityEventKindMember.Matches(File.ReadAllText(enumFile))
			.Select(m => m.Groups[1].Value)
			.ToHashSet(StringComparer.Ordinal);
	}

	/// <summary>One matrix row per declared kind, and no row for a kind that is not declared. A floor over
	/// the row count would catch a truncated table; naming the kind catches it AND says which documentation
	/// a new kind still owes.</summary>
	private static string[] MatrixKindFailures(IEnumerable<string> declaredKinds, IEnumerable<string> rowKinds)
	{
		var declared = declaredKinds.ToHashSet(StringComparer.Ordinal);
		var rows = rowKinds.ToHashSet(StringComparer.Ordinal);
		var failures = declared
			.Where(kind => !rows.Contains(kind))
			.Select(kind => $"no matrix row documents EntityEventKind.{kind}")
			.ToList();
		failures.AddRange(rows
			.Where(kind => !declared.Contains(kind))
			.Select(kind => $"the matrix documents `{kind}`, which EntityEventKind does not declare"));
		return [.. failures];
	}

	[Fact]
	public void EntityEventDispatch_AllKindsCoveredInEveryTable()
	{
		string[] dispatchFiles =
		[
			"src/CasualtiesUnknownOnline.GameAdapter/World/TrapEntityScan.cs",
			"src/CasualtiesUnknownOnline.GameAdapter/World/TrapEffectApplier.cs",
			"src/CasualtiesUnknownOnline.GameAdapter/World/TrapVisualReplay.cs"
		];

		var enumMembers = EntityEventKinds();
		Assert.True(enumMembers.Count > 0, "no enum members parsed from EntityEventKind.cs");

		var failures = new List<string>();
		foreach (var relative in dispatchFiles)
		{
			var fullPath = RepositoryPaths.File(relative);
			if (!File.Exists(fullPath))
			{
				failures.Add($"dispatch table not found: {relative}");
				continue;
			}

			var referenced = EntityEventKindReference.Matches(File.ReadAllText(fullPath))
				.Select(m => m.Groups[1].Value)
				.ToHashSet(StringComparer.Ordinal);

			foreach (var member in enumMembers)
			{
				if (!referenced.Contains(member))
				{
					failures.Add($"{relative}: EntityEventKind.{member} is not dispatched (silent default drop / never scanned)");
				}
			}

			foreach (var reference in referenced)
			{
				if (!enumMembers.Contains(reference))
				{
					failures.Add($"{relative}: references EntityEventKind.{reference} which is not in the enum (stale/typo)");
				}
			}
		}

		Assert.True(failures.Count == 0, "Entity-event dispatch gate failed" + Environment.NewLine + string.Join(Environment.NewLine, failures));
	}

	[Fact]
	public void LocalContent_IsNeverCarriedByGit()
	{
		var files = EnumerateScannedFiles().ToList();
		Assert.True(files.Count > 2000, $"git ls-files returned only {files.Count} paths - the scan surface is not the repository");

		var failures = files.Where(IsLocalContent).ToList();
		Assert.True(failures.Count == 0,
			"Local instruction files or private local artifacts are carried by git (tracked or about to be committed):"
			+ Environment.NewLine + string.Join(Environment.NewLine, failures));
	}

	[Theory]
	[InlineData("AGENTS.local.md", true)]
	[InlineData("docs/acceptance/AGENTS.local.md", true)]
	[InlineData("docs/acceptance/AGENTS.local-archive.md", true)]
	[InlineData("CLAUDE.local.md", true)]
	[InlineData(".agent-local/runtime-debug.md", true)]
	[InlineData(".acceptance/20261002-p/k-C-payload-host.log", true)]
	[InlineData("docs/handoff/plan.md", true)]
	[InlineData(".jspace/task/notes.txt", true)]
	[InlineData("AGENTS.md", false)]
	[InlineData("docs/acceptance/AGENTS.md", false)]
	[InlineData("tools/acceptance/preflight.ps1", false)]
	[InlineData("src/CasualtiesUnknownOnline.Runtime/Session/World/GenerationYield.cs", false)]
	[InlineData("tests/CasualtiesUnknownOnline.NormativeGates.Tests/RepositoryGateTests.cs", false)]
	public void LocalContentMatcher_FlagsTheNeverCommittedFamilyAndIgnoresOrdinaryPaths(string path, bool expected) =>
		Assert.Equal(expected, IsLocalContent(path));

	/// <summary>
	/// The local/private content family the red line guards: the machine-local instruction files
	/// (`AGENTS.local*` / `CLAUDE.local*`), the local notes and acceptance-artifact areas
	/// (`.agent-local/`, `.acceptance/`) and the chat-only handoff/workspace directories
	/// (`docs/handoff/`, `.jspace/`) — every one of them is declared never-committed in `.gitignore`.
	/// </summary>
	private static bool IsLocalContent(string relativePath)
	{
		var normalized = relativePath.Replace('\\', '/').TrimStart('/');
		var segments = normalized.Split('/', StringSplitOptions.RemoveEmptyEntries);
		if (segments.Length == 0)
		{
			return false;
		}

		var fileName = segments[^1];
		if (fileName.StartsWith("AGENTS.local", StringComparison.OrdinalIgnoreCase)
			|| fileName.StartsWith("CLAUDE.local", StringComparison.OrdinalIgnoreCase))
		{
			return true;
		}

		if (normalized.StartsWith("docs/handoff/", StringComparison.OrdinalIgnoreCase))
		{
			return true;
		}

		return segments[..^1].Any(segment =>
			segment.Equals(".agent-local", StringComparison.OrdinalIgnoreCase)
			|| segment.Equals(".acceptance", StringComparison.OrdinalIgnoreCase)
			|| segment.Equals(".jspace", StringComparison.OrdinalIgnoreCase));
	}

	/// <summary>
	/// The files this rule cares about: everything git would carry — tracked files AND untracked files that
	/// are not gitignored. Listing tracked files only made the gate blind to a file that is about to be
	/// committed, so a run against a dirty tree could pass while the commit that adds the file turns it red
	/// (measured twice: the adapter capability reporter's log literal, and this suite's own baseline header).
	/// </summary>
	private static IEnumerable<string> EnumerateScannedFiles()
	{
		var startInfo = new ProcessStartInfo
		{
			FileName = "git",
			Arguments = $"-C \"{RepositoryPaths.Root}\" ls-files --cached --others --exclude-standard",
			RedirectStandardOutput = true,
			RedirectStandardError = true,
			UseShellExecute = false,
			CreateNoWindow = true
		};

		using var process = Process.Start(startInfo) ?? throw new InvalidOperationException("could not start git ls-files");
		var output = process.StandardOutput.ReadToEnd();
		var error = process.StandardError.ReadToEnd();
		process.WaitForExit();
		Assert.True(process.ExitCode == 0, $"git ls-files failed{Environment.NewLine}{error}");
		return output.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries);
	}
}
