using System.Collections.Generic;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using CasualtiesUnknownOnline.Runtime.Session.World;
using CasualtiesUnknownOnline.Tests.Fakes;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.World;

/// <summary>
/// The partial block-damage registry (the late-joiner snapshot's fact
/// source): block-cell-keyed latest-damage semantics, explicit remove on
/// break/air-write, reset lifecycle, empty-table no-send, the cap and the
/// guest-side report no-op.
/// </summary>
public class BlockDamageRegistryTests
{
	[Fact]
	public void Report_LatestDamageWins_SnapshotCarriesExactCells()
	{
		using var w = ItemSimWorld.Create();
		var hostWorld = w.Host.Services.GetRequiredService<IWorldControl>();
		var received = new List<IReadOnlyList<BlockDamageEntryMsg>>();
		w.G1.Services.GetRequiredService<IWorldControl>().BlockDamageSnapshotReceived += list => received.Add(list);

		hostWorld.ReportBlockDamage(10, 20, 80f);
		hostWorld.ReportBlockDamage(10, 20, 35f); // the same cell — the latest damage wins
		hostWorld.ReportBlockDamage(0, 0, 12f); // the origin cell is a real block cell
		hostWorld.SendBlockDamageSnapshot(w.G1.SteamId);

		Assert.Single(received);
		Assert.Equal(2, received[0].Count);
		Assert.True(received[0][0].X == 10 && received[0][0].Y == 20 && received[0][0].Damage == 35f,
			$"the same-cell entry must carry the latest damage, got {received[0][0].X}/{received[0][0].Y}/{received[0][0].Damage}");
		Assert.True(received[0][1].X == 0 && received[0][1].Y == 0 && received[0][1].Damage == 12f,
			"the origin cell must survive the wire");
	}

	[Fact]
	public void Remove_ClearsTheCell()
	{
		using var w = ItemSimWorld.Create();
		var hostWorld = w.Host.Services.GetRequiredService<IWorldControl>();
		var received = new List<IReadOnlyList<BlockDamageEntryMsg>>();
		w.G1.Services.GetRequiredService<IWorldControl>().BlockDamageSnapshotReceived += list => received.Add(list);

		hostWorld.ReportBlockDamage(10, 20, 80f);
		hostWorld.RemoveBlockDamage(10, 20); // the block broke — it rides the block-state snapshot now

		hostWorld.SendBlockDamageSnapshot(w.G1.SteamId);

		Assert.Empty(received);
	}

	[Fact]
	public void Report_NonPositiveDamage_RemovesTheCell()
	{
		using var w = ItemSimWorld.Create();
		var hostWorld = w.Host.Services.GetRequiredService<IWorldControl>();
		var received = new List<IReadOnlyList<BlockDamageEntryMsg>>();
		w.G1.Services.GetRequiredService<IWorldControl>().BlockDamageSnapshotReceived += list => received.Add(list);

		hostWorld.ReportBlockDamage(10, 20, 80f);
		hostWorld.ReportBlockDamage(10, 20, 0f); // the game's state no longer has partial damage there

		hostWorld.SendBlockDamageSnapshot(w.G1.SteamId);

		Assert.Empty(received);
	}

	[Fact]
	public void EmptyTable_SendsNothing()
	{
		using var w = ItemSimWorld.Create();
		var received = new List<IReadOnlyList<BlockDamageEntryMsg>>();
		w.G1.Services.GetRequiredService<IWorldControl>().BlockDamageSnapshotReceived += list => received.Add(list);

		w.Host.Services.GetRequiredService<IWorldControl>().SendBlockDamageSnapshot(w.G1.SteamId);

		Assert.Empty(received);
	}

	[Fact]
	public void Reset_ClearsTheTable()
	{
		using var w = ItemSimWorld.Create();
		var hostWorld = w.Host.Services.GetRequiredService<IWorldControl>();
		var received = new List<IReadOnlyList<BlockDamageEntryMsg>>();
		w.G1.Services.GetRequiredService<IWorldControl>().BlockDamageSnapshotReceived += list => received.Add(list);

		hostWorld.ReportBlockDamage(10, 20, 25f);
		hostWorld.ResetDamagedBlocks(); // a new world layer is generating — the table's lifecycle

		hostWorld.SendBlockDamageSnapshot(w.G1.SteamId);

		Assert.Empty(received);
	}

	[Fact]
	public void GuestReport_IsANoOp()
	{
		using var w = ItemSimWorld.Create();
		var hostWorld = w.Host.Services.GetRequiredService<IWorldControl>();
		var guestWorld = w.G1.Services.GetRequiredService<IWorldControl>();
		var received = new List<IReadOnlyList<BlockDamageEntryMsg>>();
		w.G1.Services.GetRequiredService<IWorldControl>().BlockDamageSnapshotReceived += list => received.Add(list);

		hostWorld.ReportBlockDamage(10, 20, 25f);
		guestWorld.ReportBlockDamage(99, 99, 1f); // a guest never owns the snapshot's fact source

		hostWorld.SendBlockDamageSnapshot(w.G1.SteamId);

		Assert.Single(received);
		Assert.Single(received[0]);
		Assert.True(received[0][0].X == 10 && received[0][0].Y == 20,
			"the guest's report must not enter the host's snapshot");
	}

	[Fact]
	public void Cap_StopsNewCells_ExistingCellStillUpdates()
	{
		using var w = ItemSimWorld.Create();
		var hostWorld = w.Host.Services.GetRequiredService<IWorldControl>();
		var received = new List<IReadOnlyList<BlockDamageEntryMsg>>();
		w.G1.Services.GetRequiredService<IWorldControl>().BlockDamageSnapshotReceived += list => received.Add(list);

		// The cap lives inside the registry; drive it through the public surface
		// with distinct cells until a new cell is refused.
		var acceptedNew = 0;
		for (var i = 0; i < 300; i++)
		{
			hostWorld.ReportBlockDamage(i, 0, 1f);
			if (i < 256)
			{
				acceptedNew++;
			}
		}

		hostWorld.ReportBlockDamage(10, 0, 99f); // an existing cell keeps updating after the cap
		hostWorld.SendBlockDamageSnapshot(w.G1.SteamId);

		Assert.True(received[0].Count == acceptedNew, $"the snapshot must match the capped table (256), got {received[0].Count}");
	}

}
