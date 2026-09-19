using System.Collections.Generic;
using CasualtiesUnknownOnline.Runtime.Protocol;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using CasualtiesUnknownOnline.Runtime.Session;
using CasualtiesUnknownOnline.Runtime.Session.World;
using CasualtiesUnknownOnline.Tests.Fakes;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.World;

/// <summary>
/// The trap-layout FRESHNESS round: the member-facing table is re-derived from the
/// host's live scene at SEND time, not only on the generation edge the record is
/// written on. Without it a member that enters (or reconnects) between two scans
/// materializes an entity the world has since removed — the apply is an absolute
/// align against the snapshot and a freshly generated guest world has no such
/// entity, so the join spawns a phantom the host does not own (transient, but a
/// live hazard on the member until the next scan). The re-derive is the Runtime
/// port <see cref="ILiveTrapLayoutSource"/>; these cases drive it through the
/// fan-out's own send points rather than calling the port directly.
/// </summary>
[Trait("Category", "Integration")]
public class TrapLayoutEntryFreshnessTests
{
	private const string LivePrefab = "spikestabber";
	private const string RemovedPrefab = "turret";

	[Fact]
	public void WorldEntry_ReDerivesTheLayout_SoARemovedEntityIsNeverSent()
	{
		using var w = ItemSimWorld.Create(FakeLiveLayoutSource.Register);
		var hostWorld = w.Host.Services.GetRequiredService<IWorldControl>();
		var live = w.Host.Services.GetRequiredService<FakeLiveLayoutSource>();

		// The host's table as last derived still lists the turret, although the
		// world has since removed it (it self-destructed): the record is written on
		// the generation edge and nothing rewrites it before the next scan.
		hostWorld.ReportTrapLayout(EntityEventKind.TurretSelfDestructed, 12f, 34f, RemovedPrefab);
		// The live scene holds only what is really there.
		live.LiveScene.Add(LiveEntry());

		var received = new List<IReadOnlyList<TrapLayoutEntryMsg>>();
		w.G1.Services.GetRequiredService<IWorldControl>().TrapLayoutReceived += received.Add;

		w.G1.Session.ReportSceneState(SceneStateType.InWorld, "SampleScene");
		w.Driver.Tick(50);

		Assert.True(live.Refreshes >= 1, $"the entry send must re-derive the table first, refreshes={live.Refreshes}");
		Assert.True(received.Count == 1, $"the entry group carries the layout, got {received.Count}");
		Assert.True(received[0].Count == 1 && received[0][0].Kind == EntityEventKind.SpikeStabbed,
			"the entry group must carry the LIVE table: an entity the world has since removed must not be materialized on the entering member");
	}

	[Fact]
	public void InSessionRepair_ReDerivesTheLayout_TheSameWay()
	{
		using var w = ItemSimWorld.Create(FakeLiveLayoutSource.Register);
		var hostWorld = w.Host.Services.GetRequiredService<IWorldControl>();
		var live = w.Host.Services.GetRequiredService<FakeLiveLayoutSource>();

		// g1 is already in the world with an EMPTY layout (its entry send had
		// nothing to carry), so no further InWorld edge is coming while it stays.
		w.G1.Session.ReportSceneState(SceneStateType.InWorld, "SampleScene");
		w.Driver.Tick(50);

		hostWorld.ReportTrapLayout(EntityEventKind.TurretSelfDestructed, 12f, 34f, RemovedPrefab);
		live.LiveScene.Add(LiveEntry());

		var received = new List<IReadOnlyList<TrapLayoutEntryMsg>>();
		w.G1.Services.GetRequiredService<IWorldControl>().TrapLayoutReceived += received.Add;

		w.Host.Services.GetRequiredService<WorldEntryFanout>().SendInSessionRepair(w.G1.SteamId);
		w.Driver.Tick(50);

		Assert.True(received.Count == 1 && received[0].Count == 1 && received[0][0].Kind == EntityEventKind.SpikeStabbed,
			"the periodic repair re-derives the table too: the heal must carry the live layout, never a record the world has removed");
	}

	[Fact]
	public void EveryEnteringMember_ReceivesTheSameLiveTable()
	{
		using var w = ItemSimWorld.Create(FakeLiveLayoutSource.Register);
		var hostWorld = w.Host.Services.GetRequiredService<IWorldControl>();
		var live = w.Host.Services.GetRequiredService<FakeLiveLayoutSource>();
		hostWorld.ReportTrapLayout(EntityEventKind.TurretSelfDestructed, 12f, 34f, RemovedPrefab);
		live.LiveScene.Add(LiveEntry());

		var g1 = new List<IReadOnlyList<TrapLayoutEntryMsg>>();
		var g2 = new List<IReadOnlyList<TrapLayoutEntryMsg>>();
		w.G1.Services.GetRequiredService<IWorldControl>().TrapLayoutReceived += g1.Add;
		w.G2.Services.GetRequiredService<IWorldControl>().TrapLayoutReceived += g2.Add;

		w.G1.Session.ReportSceneState(SceneStateType.InWorld, "SampleScene");
		w.Driver.Tick(50);
		w.G2.Session.ReportSceneState(SceneStateType.InWorld, "SampleScene");
		w.Driver.Tick(50);

		Assert.True(g1.Count == 1 && g2.Count == 1, $"both entering members get the layout, got g1={g1.Count} g2={g2.Count}");
		Assert.True(g1[0].Count == 1 && g1[0][0].Kind == EntityEventKind.SpikeStabbed
			&& g2[0].Count == 1 && g2[0][0].Kind == EntityEventKind.SpikeStabbed,
			"every member converges on the same LIVE table — the third party's view is not a different one");
	}

	[Fact]
	public void AnEmptyLiveScan_KeepsTheLastDerivedTable()
	{
		using var w = ItemSimWorld.Create(FakeLiveLayoutSource.Register);
		var hostWorld = w.Host.Services.GetRequiredService<IWorldControl>();
		var live = w.Host.Services.GetRequiredService<FakeLiveLayoutSource>();
		// The fake's scene stays EMPTY: the scan sees nothing (a scene in transition).
		hostWorld.ReportTrapLayout(EntityEventKind.SpikeStabbed, -13f, 466.8f, LivePrefab);

		var received = new List<IReadOnlyList<TrapLayoutEntryMsg>>();
		w.G1.Services.GetRequiredService<IWorldControl>().TrapLayoutReceived += received.Add;

		w.Host.Services.GetRequiredService<WorldEntryFanout>().SendInSessionRepair(w.G1.SteamId);
		w.Driver.Tick(50);

		Assert.True(live.Refreshes >= 1, $"the send must attempt the re-derive, refreshes={live.Refreshes}");
		Assert.True(received.Count == 1 && received[0].Count == 1, $"the kept table still rides, got {received.Count} send(s)");
		Assert.True(received[0][0].Kind == EntityEventKind.SpikeStabbed && received[0][0].X == -13f && received[0][0].Y == 466.8f
			&& received[0][0].PrefabName == LivePrefab,
			"the KEPT entry rides with its identity intact: an empty live scan must not read as 'every trap was destroyed'");
	}

	[Fact]
	public void WithoutThePort_TheSendCarriesTheLastDerivedTable()
	{
		// A Runtime-only composition (no adapter): the live-scene port is OPTIONAL,
		// so the fan-out must be constructible and still send what the table holds.
		using var w = ItemSimWorld.Create();
		var hostWorld = w.Host.Services.GetRequiredService<IWorldControl>();
		hostWorld.ReportTrapLayout(EntityEventKind.SpikeStabbed, -13f, 466.8f, LivePrefab);

		var received = new List<IReadOnlyList<TrapLayoutEntryMsg>>();
		w.G1.Services.GetRequiredService<IWorldControl>().TrapLayoutReceived += received.Add;

		w.Host.Services.GetRequiredService<WorldEntryFanout>().SendInSessionRepair(w.G1.SteamId);
		w.Driver.Tick(50);

		Assert.True(received.Count == 1 && received[0].Count == 1,
			$"a composition without the live-scene port still sends the table as last derived, got {received.Count}");
	}

	private static TrapLayoutEntryMsg LiveEntry() => new()
	{
		Kind = EntityEventKind.SpikeStabbed,
		X = -13f,
		Y = 466.8f,
		PrefabName = LivePrefab,
	};
}
