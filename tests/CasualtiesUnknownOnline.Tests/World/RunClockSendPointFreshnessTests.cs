using System.Collections.Generic;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using CasualtiesUnknownOnline.Runtime.Session;
using CasualtiesUnknownOnline.Runtime.Session.World;
using CasualtiesUnknownOnline.Tests.Fakes;
using CasualtiesUnknownOnline.Tests.Persistence;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.World;

/// <summary>
/// The run-clock FRESHNESS round: the value the entry and repair groups carry is read off
/// the host's live world at SEND time, not taken from the last capture (the generation
/// boundary, or the host's own world entry). Without it a member that enters a run already
/// in progress is sent a total short by the whole interval since that capture — the live
/// run measured 30.0 s and 85.6 s, and the 60 s repair re-sent the same value so it never
/// converged (batch 20261001-o row 1). The read is the existing
/// <see cref="INativeWorldFacts"/> port, driven through the fan-out's own send points so
/// both the entry group and the repair group own their freshness by construction.
/// </summary>
[Trait("Category", "Integration")]
public class RunClockSendPointFreshnessTests
{
	/// <summary>The stale value a generation boundary published before the member entered.</summary>
	private const float PublishedBoundaryTotal = 100f;

	/// <summary>The live world's total when the member enters — what the member must be sent.</summary>
	private const float LiveTotal = 130f;

	private const float LayerTime = 360f;
	private const float LayerLimit = 600f;

	[Fact]
	public void WorldEntry_ReReadsTheLiveClock_SoAMidRunJoinerGetsTheCurrentTotal()
	{
		var native = new FakeNativeWorldFacts();
		native.SeedRunFields(1f, 1f, LiveTotal);
		native.SeedLayerTime(LayerTime);
		using var w = ItemSimWorld.Create(services => services.AddSingleton<INativeWorldFacts>(native));
		WorldGenerationReports.CommitRun(w.Host, layerIndex: 0);
		var hostWorld = w.Host.Services.GetRequiredService<IWorldControl>();
		// The host's last PUBLISHED value is what its generation boundary captured: exactly
		// the stale base the live run measured the joining member against (30.0 s and 85.6 s
		// short across two entries).
		hostWorld.PublishRunFacts(new RunClockFacts(PublishedBoundaryTotal, LayerTime, LayerLimit, Failure: null));

		var received = new List<RunFactsMsg>();
		w.G1.Services.GetRequiredService<IWorldControl>().RunFactsReceived += (facts, _) => received.Add(facts);

		w.G1.Session.ReportSceneState(SceneStateType.InWorld, "SampleScene");
		w.Driver.Tick(33);

		var entry = Assert.Single(received);
		Assert.True(entry.RunClockBase == LiveTotal,
			$"the entry send must carry the host's live total ({LiveTotal}), got {entry.RunClockBase} — the last published base was {PublishedBoundaryTotal}");
		Assert.Contains("capture-run-clock", native.Calls);
	}

	[Fact]
	public void InSessionRepair_ReReadsTheLiveClock_TheSameWay()
	{
		// The repair exists to recover an entry send the lazy session swallowed, and the
		// receiver maps the total onto its own world epoch — so the repair must carry the
		// live total at ITS send point too, or the member keeps the old base.
		var native = new FakeNativeWorldFacts();
		native.SeedRunFields(1f, 1f, LiveTotal);
		using var w = ItemSimWorld.Create(services => services.AddSingleton<INativeWorldFacts>(native));
		WorldGenerationReports.CommitRun(w.Host, layerIndex: 0);
		w.Host.Services.GetRequiredService<IWorldControl>().PublishRunFacts(new RunClockFacts(PublishedBoundaryTotal, LayerTime, LayerLimit, Failure: null));

		var received = new List<RunFactsMsg>();
		w.G1.Services.GetRequiredService<IWorldControl>().RunFactsReceived += (facts, _) => received.Add(facts);

		w.Host.Services.GetRequiredService<WorldEntryFanout>().SendInSessionRepair(w.G1.SteamId);
		w.Driver.Tick(33);

		var entry = Assert.Single(received);
		Assert.True(entry.RunClockBase == LiveTotal,
			$"the repair group must carry the live total too, got {entry.RunClockBase}");
	}

	[Fact]
	public void AReadThatFails_SendsNothingRatherThanAStaleTotal()
	{
		// A stale absolute total is worse than no value now that the receiver maps it onto
		// its own world epoch: the member would take it as if it described this moment.
		// Nothing is sent instead, and the next send re-reads.
		var native = new FakeNativeWorldFacts { CaptureRunClockFailure = "no live world is present" };
		using var w = ItemSimWorld.Create(services => services.AddSingleton<INativeWorldFacts>(native));
		WorldGenerationReports.CommitRun(w.Host, layerIndex: 0);
		w.Host.Services.GetRequiredService<IWorldControl>().PublishRunFacts(new RunClockFacts(LiveTotal, LayerTime, LayerLimit, Failure: null));

		var received = new List<RunFactsMsg>();
		w.G1.Services.GetRequiredService<IWorldControl>().RunFactsReceived += (facts, _) => received.Add(facts);

		w.Host.Services.GetRequiredService<WorldEntryFanout>().SendInSessionRepair(w.G1.SteamId);
		w.Driver.Tick(33);

		Assert.Empty(received);
	}
}
