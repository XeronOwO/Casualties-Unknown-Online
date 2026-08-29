using System.Collections.Generic;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using CasualtiesUnknownOnline.Runtime.Session.World;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.World;

/// <summary>
/// The damaged building-entity health registry (the late-joiner snapshot's
/// fact source): position-keyed latest-health semantics, reset lifecycle,
/// empty-table no-send, and the guest-side report no-op.
/// </summary>
public class BuildingEntityHealthRegistryTests
{
	[Fact]
	public void Report_LatestHealthWins_SnapshotCarriesCellCenters()
	{
		using var w = EntityEventSimWorld.Create();
		var hostWorld = w.Host.Services.GetRequiredService<IWorldControl>();
		var received = new List<IReadOnlyList<BuildingEntityHealthEntryMsg>>();
		w.G1.Services.GetRequiredService<WorldEntityKernelProjection>().BuildingHealthProjected += list => received.Add(list);

		hostWorld.ReportBuildingEntityHealth(10.2f, 20.8f, 80f);
		hostWorld.ReportBuildingEntityHealth(10.9f, 20.1f, 35f); // the same cell — the latest health wins
		hostWorld.ReportBuildingEntityHealth(30f, 40f, 0f); // a destroyed entity stays destroyed
		w.SendCheckpoint(w.G1);

		Assert.Single(received);
		Assert.Equal(2, received[0].Count);
		Assert.True(received[0][0].X == 10.5f && received[0][0].Y == 20.5f && received[0][0].Health == 35f,
			$"the same-cell entry must carry the latest health at the cell centre, got {received[0][0].X}/{received[0][0].Y}/{received[0][0].Health}");
		Assert.True(received[0][1].X == 30.5f && received[0][1].Y == 40.5f && received[0][1].Health == 0f,
			$"a destroyed entity's health 0 must survive the wire, got {received[0][1].X}/{received[0][1].Y}/{received[0][1].Health}");
	}

	[Fact]
	public void EmptyTable_SendsNothing()
	{
		using var w = EntityEventSimWorld.Create();
		var received = new List<IReadOnlyList<BuildingEntityHealthEntryMsg>>();
		w.G1.Services.GetRequiredService<WorldEntityKernelProjection>().BuildingHealthProjected += list => received.Add(list);

		w.SendCheckpoint(w.G1);

		Assert.Empty(received);
	}

	[Fact]
	public void Reset_ClearsTheTable()
	{
		using var w = EntityEventSimWorld.Create();
		var hostWorld = w.Host.Services.GetRequiredService<IWorldControl>();
		var received = new List<IReadOnlyList<BuildingEntityHealthEntryMsg>>();
		w.G1.Services.GetRequiredService<WorldEntityKernelProjection>().BuildingHealthProjected += list => received.Add(list);

		hostWorld.ReportBuildingEntityHealth(10f, 20f, 25f);
		hostWorld.ResetDamagedBlocks(); // a new world layer is generating — the table's lifecycle

		w.SendCheckpoint(w.G1);

		Assert.Empty(received);
	}

	[Fact]
	public void GuestReport_IsANoOp()
	{
		using var w = EntityEventSimWorld.Create();
		var hostWorld = w.Host.Services.GetRequiredService<IWorldControl>();
		var guestWorld = w.G1.Services.GetRequiredService<IWorldControl>();
		var received = new List<IReadOnlyList<BuildingEntityHealthEntryMsg>>();
		w.G1.Services.GetRequiredService<WorldEntityKernelProjection>().BuildingHealthProjected += list => received.Add(list);

		hostWorld.ReportBuildingEntityHealth(10f, 20f, 25f);
		guestWorld.ReportBuildingEntityHealth(99f, 99f, 1f); // a guest never owns the snapshot's fact source

		w.SendCheckpoint(w.G1);

		Assert.Single(received);
		Assert.Single(received[0]);
		Assert.True(received[0][0].X == 10.5f && received[0][0].Y == 20.5f,
			"the guest's report must not enter the host's snapshot");
	}
}
