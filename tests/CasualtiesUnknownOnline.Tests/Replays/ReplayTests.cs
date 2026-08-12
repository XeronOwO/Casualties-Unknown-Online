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
/// as the hand-written simulations). Item files run on the item world, the
/// entity/fluid files (phase A1) on the entity world — a file is dispatched
/// by its first action and a file in the wrong world fails instead of
/// silently running nowhere. A replay that cannot be parsed, an assertion
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
	private static readonly string[] EntityActions = ["event", "snapshot", "fluid"];

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
		if (EntityActions.Contains(steps[0].Action))
		{
			using var world = EntityEventSimWorld.Create();
			ReplayRunner.Run(fileName, world, steps, simTrace);
		}
		else
		{
			using var world = ItemSimWorld.Create();
			ReplayRunner.Run(fileName, world, steps, simTrace);
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
	public void ItemActionInEntityFile_Fails()
	{
		// A file is dispatched by its first action — mixing domains is caught by
		// the runner (the entity world knows no item actions) instead of silently
		// running part of the scenario nowhere.
		using var world = EntityEventSimWorld.Create();
		var steps = ReplayParser.Parse("@0 event g1 MineExploded 10 20\n@33 spawn g1 42 t 1.0\n", "mixed.replay");
		var e = Assert.Throws<ReplayRunner.ReplayStepException>(() => ReplayRunner.Run("mixed.replay", world, steps, new SimTrace()));
		Assert.Contains("unhandled action 'spawn'", e.Message);
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
