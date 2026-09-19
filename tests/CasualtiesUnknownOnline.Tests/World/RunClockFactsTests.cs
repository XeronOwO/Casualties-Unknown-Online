using System.Collections.Generic;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using CasualtiesUnknownOnline.Runtime.Session;
using CasualtiesUnknownOnline.Runtime.Session.World;
using CasualtiesUnknownOnline.Tests.Fakes;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.World;

/// <summary>
/// The run clock base and the layer timer on the wire (protocol 33): the two values the
/// game keeps per PROCESS and the kernel run baseline deliberately does not hold, so a
/// member that joined a run already in progress used to read only the time since it joined
/// (<c>SaveSystem.savedRunTime</c> is 0 on a fresh launch) while the host read the run's
/// total. The reading half is the adapter's and needs a running game; the Runtime seam —
/// what is stamped, what is sent, and what the receiver accepts — is driven here.
/// </summary>
[Trait("Category", "Integration")]
public class RunClockFactsTests
{
	private const float Clock = 2450.5f;
	private const float LayerTime = 360f;
	private const float LayerLimit = 600f;

	[Fact]
	public void MemberEntersWorld_ReceivesTheRunClocks()
	{
		using var w = ItemSimWorld.Create();
		WorldGenerationReports.CommitRun(w.Host, layerIndex: 0);
		var hostWorld = w.Host.Services.GetRequiredService<IWorldControl>();
		hostWorld.PublishRunFacts(Facts());

		var received = new List<(RunFactsMsg Facts, bool LayerTimerApplies)>();
		w.G1.Services.GetRequiredService<IWorldControl>().RunFactsReceived += (facts, applies) => received.Add((facts, applies));

		w.G1.Session.ReportSceneState(SceneStateType.InWorld, "SampleScene");
		w.Driver.Tick(33);

		var entry = Assert.Single(received);
		Assert.True(entry.Facts.RunClockBase == Clock, $"the host's run clock must arrive unchanged, got {entry.Facts.RunClockBase}");
		Assert.True(entry.Facts.LayerTimeSpent == LayerTime, $"the host's layer timer must arrive unchanged, got {entry.Facts.LayerTimeSpent}");
		Assert.True(entry.Facts.MaxTimePerLayer == LayerLimit, $"the host's layer limit must arrive unchanged, got {entry.Facts.MaxTimePerLayer}");
		Assert.True(entry.Facts.RunEpoch == 1UL && entry.Facts.LayerIndex == 0, "the message must carry the kernel run baseline's own stamp");
		Assert.True(entry.LayerTimerApplies, "a message stamped with this side's own generation must carry an applicable layer timer");
	}

	[Fact]
	public void StaleGeneration_KeepsTheClockAndDropsTheLayerTimer()
	{
		// The message's stamp is what the receiver compares against its OWN generation. The
		// clock is a run-scoped total, so a value that advances is still worth applying; the
		// layer timer describes one layer and must not be written onto another.
		using var w = ItemSimWorld.Create();
		WorldGenerationReports.CommitRun(w.Host, layerIndex: 0);
		var hostWorld = w.Host.Services.GetRequiredService<IWorldControl>();
		hostWorld.PublishRunFacts(Facts());

		var received = new List<(RunFactsMsg Facts, bool LayerTimerApplies)>();
		var guestWorld = w.G1.Services.GetRequiredService<IWorldControl>();
		guestWorld.RunFactsReceived += (facts, applies) => received.Add((facts, applies));

		// The receiver's own generation is whatever its kernel run baseline holds, so the
		// stale case is built from THAT stamp with another layer: the message names the
		// layer next door, which is exactly what a value captured before this side entered
		// its layer carries.
		var mine = WorldGenerationReports.StampOf(w.G1);
		guestWorld.FireRunFactsReceived(new RunFactsMsg
		{
			RunEpoch = mine.RunEpoch,
			LayerIndex = mine.LayerIndex + 1,
			RunClockBase = Clock,
			LayerTimeSpent = LayerTime,
			MaxTimePerLayer = LayerLimit,
		});

		var stale = Assert.Single(received);
		Assert.True(stale.Facts.RunClockBase == Clock, "the clock still travels — it is the run's total, not the layer's");
		Assert.False(stale.LayerTimerApplies, "a layer timer from another generation must not be applied");

		// And the receiver's own stamp applies the timer.
		guestWorld.FireRunFactsReceived(new RunFactsMsg
		{
			RunEpoch = mine.RunEpoch,
			LayerIndex = mine.LayerIndex,
			RunClockBase = Clock,
			LayerTimeSpent = LayerTime,
			MaxTimePerLayer = LayerLimit,
		});

		Assert.Equal(2, received.Count);
		Assert.True(received[1].LayerTimerApplies, "the receiver's own generation applies the layer timer");
	}

	[Fact]
	public void NoCommittedRun_SendsNothing()
	{
		// No run baseline means no identity to stamp the absolute clock with, and an
		// unstamped clock is exactly the value that could land on the wrong layer.
		using var w = ItemSimWorld.Create();
		var hostWorld = w.Host.Services.GetRequiredService<IWorldControl>();
		hostWorld.PublishRunFacts(Facts());

		var received = new List<(RunFactsMsg Facts, bool LayerTimerApplies)>();
		w.G1.Services.GetRequiredService<IWorldControl>().RunFactsReceived += (facts, applies) => received.Add((facts, applies));

		w.G1.Session.ReportSceneState(SceneStateType.InWorld, "SampleScene");
		w.Driver.Tick(33);

		Assert.Empty(received);
	}

	[Fact]
	public void NoCapturedClocks_SendsNothing()
	{
		// The host has a run but no world has been read yet: nothing is fabricated from
		// zeros, because a zero clock is a real value the receiver would write.
		using var w = ItemSimWorld.Create();
		WorldGenerationReports.CommitRun(w.Host, layerIndex: 0);

		var received = new List<(RunFactsMsg Facts, bool LayerTimerApplies)>();
		w.G1.Services.GetRequiredService<IWorldControl>().RunFactsReceived += (facts, applies) => received.Add((facts, applies));

		w.G1.Session.ReportSceneState(SceneStateType.InWorld, "SampleScene");
		w.Driver.Tick(33);

		Assert.Empty(received);
	}

	[Fact]
	public void GuestRole_NeverSends()
	{
		using var w = ItemSimWorld.Create();
		WorldGenerationReports.CommitRun(w.G1, layerIndex: 0);
		var hostWorld = w.Host.Services.GetRequiredService<IWorldControl>();
		var guestWorld = w.G1.Services.GetRequiredService<IWorldControl>();
		guestWorld.PublishRunFacts(Facts());

		var received = new List<(RunFactsMsg Facts, bool LayerTimerApplies)>();
		hostWorld.RunFactsReceived += (facts, applies) => received.Add((facts, applies));

		guestWorld.SendRunFacts(w.Host.SteamId);

		Assert.Empty(received);
	}

	[Fact]
	public void InSessionRepair_AlsoCarriesTheRunClocks()
	{
		// The entry group fires on an InWorld EDGE. A member that stays continuously in the
		// world after a swallowed send has no second edge, so the 60 s repair group must
		// carry the same absolute clocks — otherwise a guest that missed the entry message
		// keeps its own zero clock for the rest of the run.
		using var w = ItemSimWorld.Create();
		WorldGenerationReports.CommitRun(w.Host, layerIndex: 0);
		var hostWorld = w.Host.Services.GetRequiredService<IWorldControl>();
		hostWorld.PublishRunFacts(Facts());

		var received = new List<(RunFactsMsg Facts, bool LayerTimerApplies)>();
		w.G1.Services.GetRequiredService<IWorldControl>().RunFactsReceived += (facts, applies) => received.Add((facts, applies));

		w.Host.Services.GetRequiredService<WorldEntryFanout>().SendInSessionRepair(w.G1.SteamId);

		var entry = Assert.Single(received);
		Assert.True(entry.Facts.RunClockBase == Clock, $"the repair group must carry the host's clock, got {entry.Facts.RunClockBase}");
		Assert.True(entry.Facts.LayerTimeSpent == LayerTime, "the repair group must carry the layer timer too");
	}

	[Fact]
	public void UnknownStamp_AppliesTheLayerTimer()
	{
		// A receiver that has no kernel run baseline yet cannot compare the stamp (Unknown),
		// and the timer is still applied: the write guard is monotone, so accepting an
		// absolute value from the only host this connection has is safe, and refusing it
		// would leave a freshly-joined member's radiation timer at zero until its own
		// baseline arrives. This is the documented reading of "not this side's generation".
		using var w = ItemSimWorld.Create();
		var received = new List<(RunFactsMsg Facts, bool LayerTimerApplies)>();
		var guestWorld = w.G1.Services.GetRequiredService<IWorldControl>();
		guestWorld.RunFactsReceived += (facts, applies) => received.Add((facts, applies));

		guestWorld.FireRunFactsReceived(new RunFactsMsg
		{
			RunEpoch = 1UL,
			LayerIndex = 0,
			RunClockBase = Clock,
			LayerTimeSpent = LayerTime,
			MaxTimePerLayer = LayerLimit,
		});

		var unknown = Assert.Single(received);
		Assert.True(unknown.LayerTimerApplies, "with no run baseline of its own the receiver cannot call the stamp stale, so the timer is applied");
	}

	private static RunClockFacts Facts() => new(Clock, LayerTime, LayerLimit, Failure: null);
}
