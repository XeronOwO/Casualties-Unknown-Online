using System.Collections.Generic;
using CasualtiesUnknownOnline.GameState;
using CasualtiesUnknownOnline.Runtime.Protocol;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using CasualtiesUnknownOnline.Runtime.Session;
using CasualtiesUnknownOnline.Runtime.Session.Items;
using CasualtiesUnknownOnline.Runtime.Session.World;
using CasualtiesUnknownOnline.Tests.Fakes;
using CasualtiesUnknownOnline.Tests.Persistence;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.World;

/// <summary>
/// The trap-layout authority flow (host → guest): the host records the
/// generated trap entities on the generation-finished edge, the world-entry
/// snapshot carries them, the guest aligns its regenerated world (the entity
/// distribution runs physics queries outside the random isolation — the
/// sides' layouts diverge while the block fingerprint stays identical).
/// </summary>
[Trait("Category", "Integration")]
public class TrapLayoutSimulationTests
{
	[Fact]
	public void WorldEntrySnapshot_CarriesTheLayoutEntries()
	{
		using var w = ItemSimWorld.Create();
		var received = new List<IReadOnlyList<TrapLayoutEntryMsg>>();
		w.G1.Services.GetRequiredService<IWorldControl>().TrapLayoutReceived += received.Add;

		w.Host.Services.GetRequiredService<IWorldControl>().ReportTrapLayout(EntityEventKind.SpikeStabbed, -13f, 466.8f, "spikestabber");
		w.Host.Services.GetRequiredService<IWorldControl>().ReportTrapLayout(EntityEventKind.GeyserActivated, 30f, 40f, "geyser");
		w.Host.Services.GetRequiredService<IWorldControl>().SendTrapLayoutSnapshot(w.G1.SteamId);
		w.Driver.Tick(50);

		Assert.True(received.Count == 1, $"the layout snapshot must arrive, got {received.Count}");
		Assert.True(received[0].Count == 2, $"every entry rides, got {received[0].Count}");
		Assert.True(received[0][0].Kind == EntityEventKind.SpikeStabbed && received[0][0].X == -13f && received[0][0].PrefabName == "spikestabber",
			"the kind, the position and the prefab name ride intact");
	}

	[Fact]
	public void WorldEntrySnapshot_CarriesTheCreationKey_OfARuntimeCreatedTrap()
	{
		// The host scans a runtime-created trap and the layout entry must carry
		// its creation identity end-to-end; otherwise the guest's materialized
		// copy stays markerless and the runtime-entity snapshot creates a SECOND
		// copy at the creation position (the 1 m positional fallback is gone).
		using var w = ItemSimWorld.Create();
		var received = new List<IReadOnlyList<TrapLayoutEntryMsg>>();
		w.G1.Services.GetRequiredService<IWorldControl>().TrapLayoutReceived += received.Add;
		var creationKey = new RuntimeEntityKeyMsg
		{
			Id = "spikestabber",
			CellX = -13,
			CellY = 466,
			CreatorSteamId = 2001,
			CreationSequence = 4,
		};

		w.Host.Services.GetRequiredService<IWorldControl>().ReportTrapLayout(EntityEventKind.SpikeStabbed, -13f, 466.8f, "spikestabber", creationKey);
		w.Host.Services.GetRequiredService<IWorldControl>().ReportTrapLayout(EntityEventKind.GeyserActivated, 30f, 40f, "geyser");
		w.Host.Services.GetRequiredService<IWorldControl>().SendTrapLayoutSnapshot(w.G1.SteamId);
		w.Driver.Tick(50);

		Assert.True(received.Count == 1, $"the layout snapshot must arrive, got {received.Count}");
		var keyed = received[0][0];
		Assert.NotNull(keyed.CreationKey);
		Assert.Equal("spikestabber", keyed.CreationKey.Id);
		Assert.Equal(-13, keyed.CreationKey.CellX);
		Assert.Equal(466, keyed.CreationKey.CellY);
		Assert.Equal(2001ul, keyed.CreationKey.CreatorSteamId);
		Assert.Equal(4u, keyed.CreationKey.CreationSequence);
		Assert.Null(received[0][1].CreationKey); // a generated trap has no creation record
	}

	[Fact]
	public void WorldEntrySnapshot_EmptyLayout_SendsNothing()
	{
		using var w = ItemSimWorld.Create();
		var received = new List<IReadOnlyList<TrapLayoutEntryMsg>>();
		w.G1.Services.GetRequiredService<IWorldControl>().TrapLayoutReceived += received.Add;

		w.Host.Services.GetRequiredService<IWorldControl>().SendTrapLayoutSnapshot(w.G1.SteamId);
		w.Driver.Tick(50);

		Assert.True(received.Count == 0, $"an empty layout sends nothing, got {received.Count}");
	}

	[Fact]
	public void NewLayer_ResetsTheLayout()
	{
		using var w = ItemSimWorld.Create();
		w.Host.Services.GetRequiredService<IWorldControl>().ReportTrapLayout(EntityEventKind.SpikeStabbed, 1f, 2f, "spikestabber");

		w.Host.Services.GetRequiredService<IWorldControl>().ResetDamagedBlocks(); // a new layer is generating — all three world tables reset together

		var received = new List<IReadOnlyList<TrapLayoutEntryMsg>>();
		w.G1.Services.GetRequiredService<IWorldControl>().TrapLayoutReceived += received.Add;
		w.Host.Services.GetRequiredService<IWorldControl>().SendTrapLayoutSnapshot(w.G1.SteamId);
		w.Driver.Tick(50);

		Assert.True(received.Count == 0, $"the new layer's layout starts empty, got {received.Count}");
	}

	[Fact]
	public void SameKindSameCell_LatestEntryWins()
	{
		using var w = ItemSimWorld.Create();
		var received = new List<IReadOnlyList<TrapLayoutEntryMsg>>();
		w.G1.Services.GetRequiredService<IWorldControl>().TrapLayoutReceived += received.Add;

		w.Host.Services.GetRequiredService<IWorldControl>().ReportTrapLayout(EntityEventKind.SpikeStabbed, 10.4f, 20.4f, "spikestabber");
		w.Host.Services.GetRequiredService<IWorldControl>().ReportTrapLayout(EntityEventKind.SpikeStabbed, 10.6f, 20.6f, "spikestabber"); // the same cell — the latest fact wins
		w.Host.Services.GetRequiredService<IWorldControl>().SendTrapLayoutSnapshot(w.G1.SteamId);
		w.Driver.Tick(50);

		Assert.True(received.Count == 1 && received[0].Count == 1, $"one cell holds one entry, got {received.Count} snapshot(s)");
		Assert.True(received[0][0].X == 10.6f, $"the latest entry wins, got X={received[0][0].X}");
	}

	[Fact]
	public void InSessionRepair_DeliversTheLayout_WithoutAWorldEntryEdge()
	{
		using var w = ItemSimWorld.Create();

		// g1 enters the world BEFORE the layer's scan is recorded (or its entry
		// send was swallowed): no further InWorld edge is coming while it stays in
		// the world, so the in-session repair is the only path left that can still
		// deliver the layout (W6).
		w.G1.Session.ReportSceneState(SceneStateType.InWorld, "SampleScene");
		w.Driver.Tick(33);

		var received = new List<IReadOnlyList<TrapLayoutEntryMsg>>();
		w.G1.Services.GetRequiredService<IWorldControl>().TrapLayoutReceived += received.Add;

		w.Host.Services.GetRequiredService<IWorldControl>().ReportTrapLayout(EntityEventKind.SpikeStabbed, -13f, 466.8f, "spikestabber");
		w.Host.Services.GetRequiredService<WorldEntryFanout>().SendInSessionRepair(w.G1.SteamId);
		w.Driver.Tick(50);

		Assert.True(received.Count == 1, $"the in-session repair must deliver the layout, got {received.Count}");
		Assert.True(received[0].Count == 1 && received[0][0].Kind == EntityEventKind.SpikeStabbed
			&& received[0][0].X == -13f && received[0][0].Y == 466.8f && received[0][0].PrefabName == "spikestabber",
			"the entry recorded after the entry group went out must still reach the member intact");
	}

	[Fact]
	public void WithoutTheRepairCall_TheEntryEdgeAloneNeverDeliversTheLayout()
	{
		// The gap this cycle closes, pinned as a counterfactual: a member that
		// entered the world before the scan was recorded receives NOTHING until an
		// in-session repair runs — no live broadcast, no second InWorld edge.
		using var w = ItemSimWorld.Create();
		w.G1.Session.ReportSceneState(SceneStateType.InWorld, "SampleScene");
		w.Driver.Tick(33);

		var received = new List<IReadOnlyList<TrapLayoutEntryMsg>>();
		w.G1.Services.GetRequiredService<IWorldControl>().TrapLayoutReceived += received.Add;

		w.Host.Services.GetRequiredService<IWorldControl>().ReportTrapLayout(EntityEventKind.SpikeStabbed, -13f, 466.8f, "spikestabber");
		w.Driver.Tick(50);

		Assert.True(received.Count == 0, $"recording the layout is not a send — only a repair delivers it, got {received.Count}");
	}

	[Fact]
	public void InSessionRepair_CarriesEveryAbsoluteTableTheCycleOwned()
	{
		var native = new FakeNativeWorldFacts();
		native.SeedBlockDamage(5, 6, 70f);
		using var w = ItemSimWorld.Create(services => services.AddSingleton<INativeWorldFacts>(native));
		var hostWorld = w.Host.Services.GetRequiredService<IWorldControl>();
		hostWorld.ReportBlockState(5, 6, 7);
		hostWorld.ReportTrapLayout(EntityEventKind.SpikeStabbed, -13f, 466.8f, "spikestabber");
		hostWorld.SendEntitySpawned(Creation("landmine", 7f, 8f));
		var checkpoints = new List<GameCheckpoint>();
		w.G1.Services.GetRequiredService<ItemKernelAuthority>().CheckpointRestored += checkpoints.Add;

		w.Host.Services.GetRequiredService<WorldEntryFanout>().SendInSessionRepair(w.G1.SteamId);
		w.Driver.Tick(50);

		Assert.True(w.ReceivedCount(w.G1, NetMsg.WorldBlockState) == 1, "the repair carries the block-state snapshot");
		Assert.True(w.ReceivedCount(w.G1, NetMsg.BlockDamageSnapshot) == 1, "the repair carries the block-damage snapshot");
		Assert.True(w.ReceivedCount(w.G1, NetMsg.TrapLayoutSnapshot) == 1, "the repair carries the trap layout");
		Assert.True(w.ReceivedCount(w.G1, NetMsg.RuntimeEntitySnapshot) == 1, "the repair carries the runtime-entity snapshot");
		Assert.True(checkpoints.Count == 1, $"the kernel envelope must be a real CHECKPOINT (applied on the guest), got {checkpoints.Count} checkpoint restore(s)");
	}

	[Fact]
	public void ReplaceLayout_DropsTheRemovedEntity_SoTheRepairCannotResurrectIt()
	{
		using var w = ItemSimWorld.Create();
		var hostWorld = w.Host.Services.GetRequiredService<IWorldControl>();
		hostWorld.ReportTrapLayout(EntityEventKind.TurretFired, 10f, 20f, "turret");
		hostWorld.ReportTrapLayout(EntityEventKind.SpikeStabbed, 30f, 40f, "spikestabber");

		var received = new List<IReadOnlyList<TrapLayoutEntryMsg>>();
		w.G1.Services.GetRequiredService<IWorldControl>().TrapLayoutReceived += received.Add;

		// The turret self-destructed: the live scan no longer sees it, so the next
		// repair must not put it back on any peer.
		Assert.True(
			hostWorld.ReplaceTrapLayout([new TrapLayoutEntryMsg { Kind = EntityEventKind.SpikeStabbed, X = 30f, Y = 40f, PrefabName = "spikestabber" }]),
			"a non-empty scan against the table replaces it");
		w.Host.Services.GetRequiredService<WorldEntryFanout>().SendInSessionRepair(w.G1.SteamId);
		w.Driver.Tick(50);

		Assert.True(received.Count == 1, $"one snapshot rides the repair, got {received.Count}");
		Assert.True(received[0].Count == 1, $"only the surviving entry rides, got {received[0].Count} entr(ies)");
		var survivor = received[0][0];
		Assert.True(survivor.Kind == EntityEventKind.SpikeStabbed && survivor.X == 30f && survivor.Y == 40f && survivor.PrefabName == "spikestabber",
			$"the surviving entry must be the spike itself, got {survivor.Kind} at ({survivor.X},{survivor.Y}) '{survivor.PrefabName}'");
	}

	[Fact]
	public void ReplaceLayout_EmptyScanAgainstANonEmptyTable_KeepsTheTable()
	{
		using var w = ItemSimWorld.Create();
		var hostWorld = w.Host.Services.GetRequiredService<IWorldControl>();
		hostWorld.ReportTrapLayout(EntityEventKind.SpikeStabbed, 30f, 40f, "spikestabber");

		var received = new List<IReadOnlyList<TrapLayoutEntryMsg>>();
		w.G1.Services.GetRequiredService<IWorldControl>().TrapLayoutReceived += received.Add;

		// A scan that sees nothing (objects inactive or unloaded mid-transition) is
		// not a destroyed layer: wiping the table would make every guest destroy
		// its whole layout, so the fail-safe refuses it and keeps the last table.
		Assert.False(hostWorld.ReplaceTrapLayout([]), "the empty-scan fail-safe must REFUSE the replace");
		w.Host.Services.GetRequiredService<WorldEntryFanout>().SendInSessionRepair(w.G1.SteamId);
		w.Driver.Tick(50);

		Assert.True(received.Count == 1, $"the repair still sends the kept table, got {received.Count}");
		Assert.True(received[0].Count == 1, $"the kept table still holds the entry, got {received[0].Count} entr(ies)");
		var kept = received[0][0];
		Assert.True(kept.Kind == EntityEventKind.SpikeStabbed && kept.X == 30f && kept.Y == 40f && kept.PrefabName == "spikestabber",
			$"the kept entry must be the original one, got {kept.Kind} at ({kept.X},{kept.Y}) '{kept.PrefabName}'");
	}

	[Fact]
	public void InSessionRepair_EmptyLayout_SendsNothing()
	{
		using var w = ItemSimWorld.Create();
		var received = new List<IReadOnlyList<TrapLayoutEntryMsg>>();
		w.G1.Services.GetRequiredService<IWorldControl>().TrapLayoutReceived += received.Add;

		w.Host.Services.GetRequiredService<WorldEntryFanout>().SendInSessionRepair(w.G1.SteamId);
		w.Driver.Tick(50);

		Assert.True(received.Count == 0, $"an empty layout sends nothing, got {received.Count}");
	}

	private static EntitySpawnedMsg Creation(string id, float x, float y) => new()
	{
		Id = id,
		Position = new NetVector2Msg(x, y),
		Rotation = 0f,
	};
}
