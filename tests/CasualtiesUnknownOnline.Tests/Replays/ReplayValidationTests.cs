using System.Linq;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Replays;

/// <summary>
/// Runner-level semantic validation (the same file:line shape as the parser)
/// plus the folder-level domain-integrity guard: a replay file that mixes
/// exclusive domains must never be silently accepted by the domain split — it
/// has no world and must fail loudly, so the guard asserts the folder holds no
/// mixed file. The per-file domain classes carry the scenario regressions.
/// </summary>
public class ReplayValidationTests
{
	[Fact]
	public void UnknownEntityKind_Fails()
	{
		using var world = World.EntityEventSimWorld.Create();
		var steps = ReplayParser.Parse("@0 event g1 Nope 10 20\n", "bad.replay");
		var e = Assert.Throws<ReplayRunner.ReplayStepException>(() => ReplayRunner.Run("bad.replay", world, steps, new SimTrace()));
		Assert.Contains("unknown entity kind 'Nope'", e.Message);
		Assert.StartsWith("bad.replay:1:", e.Message);
	}

	[Fact]
	public void FluidRunsMismatch_Fails()
	{
		using var world = World.EntityEventSimWorld.Create();
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
		// ReplayHarness.DomainOf classifies a file mixing exclusive domains as
		// "mixed"; driving the mixed steps directly against the entity world
		// must also fail loudly instead of silently running part of the
		// scenario nowhere.
		using var world = World.EntityEventSimWorld.Create();
		var steps = ReplayParser.Parse("@0 event g1 MineExploded 10 20\n@33 spawn g1 42 t 1.0\n", "mixed.replay");
		var e = Assert.Throws<ReplayRunner.ReplayStepException>(() => ReplayRunner.Run("mixed.replay", world, steps, new SimTrace()));
		Assert.Contains("unhandled action 'spawn'", e.Message);
	}

	[Fact]
	public void NoReplayFile_MixesExclusiveDomains()
	{
		var mixed = ReplayHarness.FilesOfDomain("mixed").Select(row => (string)row[0]).ToList();
		Assert.True(mixed.Count == 0,
			$"a replay file must run on exactly one world; mixed-domain files: [{string.Join(", ", mixed)}]");
	}
}
