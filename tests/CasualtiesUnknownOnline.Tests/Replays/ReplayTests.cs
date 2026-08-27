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
/// Phase-4 replay regression: every *.replay file under Replays/ is one
/// automated scenario — a real bug's operation sequence fossilized as data
/// (with its OperationTrace provenance in the file's comments), driven over
/// the full simulated stack (TestNode + FakeNetwork, same injection helpers
/// as the hand-written simulations). A file is dispatched by its exclusive
/// domain actions (item / entity+fluid / block-break / trade); shared actions
/// (fault, clearfault, expect) do not decide the world. A file mixing
/// exclusive domains fails instead of silently running part of the scenario
/// nowhere. A replay that cannot be parsed, an assertion
/// that never converges or an expectation the world violates fails this test
/// with the file:line of the offending step. Every run also emits its SimTrace
/// (the OperationTrace-format result sequence) to SimTraces/ and asserts the
/// trace's contract: every action resolved (no begin-without-end leak, the
/// production baseline semantic) and every line is extractable by
/// tools/extract-itemtrace.ps1's normalization regexes — the simulated trace
/// is diffable against the game's real trace of the same gesture sequence.
/// </summary>
public class ReplayTests
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

	public static IEnumerable<object[]> ReplayFiles() =>
		Directory.EnumerateFiles(Path.Combine(AppContext.BaseDirectory, "Replays"), "*.replay")
			.Select(path => new object[] { Path.GetFileName(path) })
			.OrderBy(args => (string)args[0]);

	[Theory]
	[MemberData(nameof(ReplayFiles))]
	public void Replay(string fileName)
	{
		var path = Path.Combine(AppContext.BaseDirectory, "Replays", fileName);
		var steps = ReplayParser.Parse(File.ReadAllText(path), fileName);
		var simTrace = new SimTrace();
		var domain = SelectDomain(fileName, steps);
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
			default:
				using (var world = ItemSimWorld.Create())
				{
					ReplayRunner.Run(fileName, world, steps, simTrace);
					var shadowDiff = world.CompareKernelShadow();
					Assert.True(!shadowDiff.HasDifferences,
						$"{fileName}: kernel shadow semantic diff: {string.Join(" | ", shadowDiff.Differences)}");
				}

				break;
		}

		AssertSimTraceContract(fileName, simTrace);
	}

	// ===== Semantic-validation facts (runner-level, same file:line shape as the parser) =====

	[Fact]
	public void UnknownEntityKind_Fails()
	{
		using var world = EntityEventSimWorld.Create();
		var steps = ReplayParser.Parse("@0 event g1 Nope 10 20\n", "bad.replay");
		var e = Assert.Throws<ReplayRunner.ReplayStepException>(() => ReplayRunner.Run("bad.replay", world, steps, new SimTrace()));
		Assert.Contains("unknown entity kind 'Nope'", e.Message);
		Assert.StartsWith("bad.replay:1:", e.Message);
	}

	[Fact]
	public void FluidRunsMismatch_Fails()
	{
		using var world = EntityEventSimWorld.Create();
		var steps = ReplayParser.Parse("@0 fluid g1 0 0 4 1 10\n", "bad.replay"); // 4x1 needs value/count pairs, 1 byte is not a pair
		var e = Assert.Throws<ReplayRunner.ReplayStepException>(() => ReplayRunner.Run("bad.replay", world, steps, new SimTrace()));
		Assert.Contains("RLE runs", e.Message);
	}

	[Fact]
	public void BlockBreakWithoutDrops_Fails()
	{
		using var world = BlockBreakReplayWorld.Create();
		var steps = ReplayParser.Parse("@0 break g1 5 7\n", "bad-block.replay");
		var e = Assert.Throws<ReplayRunner.ReplayStepException>(() => ReplayRunner.Run("bad-block.replay", world, steps, new SimTrace()));
		Assert.Contains("break needs drops", e.Message);
	}

	[Fact]
	public void TradeActionFromHost_Fails()
	{
		using var world = TradeReplayWorld.Create();
		var steps = ReplayParser.Parse("@0 trade host meet\n", "bad-trade.replay");
		var e = Assert.Throws<ReplayRunner.ReplayStepException>(() => ReplayRunner.Run("bad-trade.replay", world, steps, new SimTrace()));
		Assert.Contains("trade actions must originate from g1", e.Message);
	}

	[Fact]
	public void ItemActionInEntityFile_Fails()
	{
		// ReplayTests.SelectDomain refuses a file mixing exclusive domains; driving
		// the mixed steps directly against the entity world must also fail loudly
		// instead of silently running part of the scenario nowhere.
		using var world = EntityEventSimWorld.Create();
		var steps = ReplayParser.Parse("@0 event g1 MineExploded 10 20\n@33 spawn g1 42 t 1.0\n", "mixed.replay");
		var e = Assert.Throws<ReplayRunner.ReplayStepException>(() => ReplayRunner.Run("mixed.replay", world, steps, new SimTrace()));
		Assert.Contains("unhandled action 'spawn'", e.Message);
	}

	private static string SelectDomain(string fileName, ReplayStep[] steps)
	{
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

		if (domains.Count > 1)
		{
			throw new InvalidOperationException($"{fileName}: mixes exclusive replay domains [{string.Join(", ", domains)}] — one file runs on one world");
		}

		return domains.Count == 1 ? domains[0] : "item";
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
