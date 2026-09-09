using System.Collections.Generic;
using CasualtiesUnknownOnline.Runtime.Protocol;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using CasualtiesUnknownOnline.Runtime.Session.World;
using CasualtiesUnknownOnline.Tests.Fakes;
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
}
