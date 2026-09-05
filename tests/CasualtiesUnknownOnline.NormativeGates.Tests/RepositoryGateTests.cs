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

		foreach (var file in EnumerateTrackedFiles())
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

		Assert.True(failures.Count == 0, "Absolute machine paths found in tracked files" + Environment.NewLine + string.Join(Environment.NewLine, failures));
	}

	[Fact]
	public void EventReplayMatrix_Completeness()
	{
		var path = RepositoryPaths.File("docs/event-replay-matrix.csv");
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

		Assert.True(failures.Count == 0, "Event-replay gate failed" + Environment.NewLine + string.Join(Environment.NewLine, failures));
	}

	[Fact]
	public void EntityEventDispatch_AllKindsCoveredInEveryTable()
	{
		var root = RepositoryPaths.Root;
		var enumFile = RepositoryPaths.File("src/CasualtiesUnknownOnline.Runtime/Protocol/EntityEventKind.cs");
		string[] dispatchFiles =
		[
			"src/CasualtiesUnknownOnline.GameAdapter/World/TrapEntityScan.cs",
			"src/CasualtiesUnknownOnline.GameAdapter/World/TrapEffectApplier.cs",
			"src/CasualtiesUnknownOnline.GameAdapter/World/TrapVisualReplay.cs"
		];

		Assert.True(File.Exists(enumFile), "EntityEventKind.cs missing");
		var enumMembers = EntityEventKindMember.Matches(File.ReadAllText(enumFile))
			.Select(m => m.Groups[1].Value)
			.ToHashSet(StringComparer.Ordinal);
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
	public void DeliveryChecklist_NoIncompleteRequiredBoxes()
	{
		var path = RepositoryPaths.File("docs/evidence/delivery-checklist.md");
		Assert.True(File.Exists(path), "delivery checklist missing");
		var lines = File.ReadAllLines(path);

		var failures = new List<string>();
		var checkedCount = 0;

		foreach (var line in lines)
		{
			if (line.StartsWith("- [ ]", StringComparison.Ordinal) || line.TrimStart().StartsWith("- [ ]", StringComparison.Ordinal))
			{
				if (line.Contains("FORBIDDEN", StringComparison.Ordinal) || line.Contains("Release-cycle deployment/acceptance", StringComparison.Ordinal))
				{
					continue;
				}

				failures.Add(line.Trim());
			}
			else if (line.TrimStart().StartsWith("- [x]", StringComparison.Ordinal))
			{
				checkedCount++;
				if (line.Contains("FORBIDDEN", StringComparison.Ordinal))
				{
					failures.Add($"FORBIDDEN box checked: {line.Trim()}");
				}
			}
		}

		Assert.True(failures.Count == 0, $"Delivery gate failed ({failures.Count} issue(s), {checkedCount} boxes checked)" + Environment.NewLine + string.Join(Environment.NewLine, failures));
	}

	private static IEnumerable<string> EnumerateTrackedFiles()
	{
		var startInfo = new ProcessStartInfo
		{
			FileName = "git",
			Arguments = $"-C \"{RepositoryPaths.Root}\" ls-files",
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
