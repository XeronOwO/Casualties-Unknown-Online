using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using CasualtiesUnknownOnline.Tests.Fakes;
using CasualtiesUnknownOnline.Tests.World;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Replays;

/// <summary>
/// The stateless replay runner shared by the domain-split replay test classes
/// (item / entity / block-break / trade): file enumeration + domain
/// classification, the world dispatch, the kernel-diff assertion and the
/// SimTrace contract. It holds no state; every call builds a fresh world, so
/// the four classes can run in parallel collections.
/// </summary>
internal static class ReplayHarness
{
	private static readonly string[] ItemActions = ["spawn", "pickup", "drop", "use", "slot", "destroy", "craft", "cook", "expect_no_reject"];

	private static readonly string[] EntityActions = ["event", "snapshot", "fluid"];

	private static readonly string[] BlockBreakActions = ["airwrite", "break"];

	private static readonly string[] TradeActions = ["trade"];

	// The extract-itemtrace.ps1 normalization regexes (:16-28) — the SimTrace
	// format contract: an END line ("op=N result=X events=[..]") or a begin
	// line ("op=N begin"), nothing else.
	private static readonly Regex EndRegex = new(@"op=(\d+) .*result=([^ ]+).*events=\[([^\]]*)\]");
	private static readonly Regex BeginRegex = new(@"op=(\d+) begin ");

	/// <summary>The replay files of one exclusive domain, ordered by name.</summary>
	internal static IEnumerable<object[]> FilesOfDomain(string domain) =>
		Directory.EnumerateFiles(Path.Combine(AppContext.BaseDirectory, "Replays"), "*.replay")
			.Select(path => Path.GetFileName(path))
			.Where(fileName => DomainOf(fileName) == domain)
			.OrderBy(fileName => fileName)
			.Select(fileName => new object[] { fileName });

	/// <summary>
	/// The exclusive domain of a replay file ("item" / "entity" / "block-break" /
	/// "trade"), or "mixed" when the file mixes exclusive domains — a file must
	/// run on exactly one world, never silently part of the scenario nowhere.
	/// </summary>
	internal static string DomainOf(string fileName)
	{
		var steps = ReplayParser.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Replays", fileName)), fileName);
		var domains = new List<string>();
		if (steps.Any(step => ItemActions.Contains(step.Action)))
		{
			domains.Add("item");
		}

		if (steps.Any(step => EntityActions.Contains(step.Action)))
		{
			domains.Add("entity");
		}

		if (steps.Any(step => BlockBreakActions.Contains(step.Action)))
		{
			domains.Add("block-break");
		}

		if (steps.Any(step => TradeActions.Contains(step.Action)))
		{
			domains.Add("trade");
		}

		return domains.Count switch
		{
			0 => "item",
			1 => domains[0],
			_ => "mixed",
		};
	}

	/// <summary>Drives one replay file over its domain world and asserts the full contract.</summary>
	internal static void Run(string fileName)
	{
		var path = Path.Combine(AppContext.BaseDirectory, "Replays", fileName);
		var steps = ReplayParser.Parse(File.ReadAllText(path), fileName);
		var simTrace = new SimTrace();
		var domain = DomainOf(fileName);
		switch (domain)
		{
			case "entity":
				using (var world = EntityEventSimWorld.Create())
				{
					ReplayRunner.Run(fileName, world, steps, simTrace);
				}

				break;
			case "block-break":
				using (var world = BlockBreakReplayWorld.Create())
				{
					ReplayRunner.Run(fileName, world, steps, simTrace);
				}

				break;
			case "trade":
				using (var world = TradeReplayWorld.Create())
				{
					ReplayRunner.Run(fileName, world, steps, simTrace);
				}

				break;
			case "item":
				using (var world = ItemSimWorld.Create())
				{
					ReplayRunner.Run(fileName, world, steps, simTrace);
					var kernelDiff = world.CompareKernel();
					Assert.True(!kernelDiff.HasDifferences,
						$"{fileName}: kernel semantic diff: {string.Join(" | ", kernelDiff.Differences)}");
				}

				break;
			default:
				throw new InvalidOperationException($"{fileName}: mixes exclusive replay domains — one file runs on one world");
		}

		AssertSimTraceContract(fileName, simTrace);
	}

	private static void AssertSimTraceContract(string fileName, SimTrace simTrace)
	{
		Assert.False(simTrace.HasPendingOps,
			$"{fileName}: every action must resolve — a begin without its end is the leak fingerprint (the OperationTrace baseline semantic, OperationTrace.cs:14-16)");

		var tracePath = ReplayRunner.SimTracePath(fileName);
		Assert.True(File.Exists(tracePath), $"{fileName}: SimTrace file missing at {tracePath}");
		Assert.True(new FileInfo(tracePath).Length > 0, $"{fileName}: SimTrace file is empty");

		foreach (var line in simTrace.Lines)
		{
			Assert.True(EndRegex.IsMatch(line) || BeginRegex.IsMatch(line),
				$"{fileName}: SimTrace line is not extractable by extract-itemtrace.ps1's regexes (the diff contract): '{line}'");
		}
	}
}
