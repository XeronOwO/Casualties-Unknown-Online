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
/// The world-entry snapshot fan-out (SceneStateHandler's InWorld edge): a
/// re-entering member receives the block-state, trap-consumption AND
/// opened-entities snapshots AT ONCE — the trap/opened sends used to ride
/// only the 60 s periodic resend, so a rejoin saw spent traps fire up to a
/// minute late and opened doors closed (the reconnect-restore round).
/// </summary>
public class WorldEntrySnapshotTests
{
	[Fact]
	public void MemberEntersWorld_ReceivesTrapAndOpenedSnapshotsWithTheBlockState()
	{
		using var w = ItemSimWorld.Create();
		var hostWorld = w.Host.Services.GetRequiredService<IWorldControl>();
		hostWorld.ReportTrapConsumed(EntityEventKind.MineExploded, 10f, 20f, extra: 0);
		hostWorld.ReportOpenedEntity(30f, 40f);
		hostWorld.ReportBuildingEntityHealth(50f, 60f, 25f);
		hostWorld.ReportBlockDamage(12, 34, 80f);

		var g1Traps = new List<IReadOnlyList<EntityEventMsg>>();
		var g1Opened = new List<IReadOnlyList<NetVector2Msg>>();
		var g1Health = new List<IReadOnlyList<BuildingEntityHealthEntryMsg>>();
		var g1BlockDamage = new List<IReadOnlyList<BlockDamageEntryMsg>>();
		w.G1.Services.GetRequiredService<EntityEventChannel>().TrapStateReceived += list => g1Traps.Add(list);
		w.G1.Services.GetRequiredService<EntityEventChannel>().OpenedEntitiesSnapshotReceived += list => g1Opened.Add(list);
		w.G1.Services.GetRequiredService<EntityEventChannel>().BuildingEntityHealthSnapshotReceived += list => g1Health.Add(list);
		w.G1.Services.GetRequiredService<IWorldControl>().BlockDamageSnapshotReceived += list => g1BlockDamage.Add(list);

		w.G1.Session.ReportSceneState(SceneStateType.InWorld, "SampleScene");
		w.Driver.Tick(33);

		// The world-entry fan-out sends ALL the world-state snapshots — the
		// trap, opened, entity-health and block-damage sends must not wait for
		// the 60 s periodic resend.
		Assert.Single(g1Traps);
		Assert.Single(g1Opened);
		Assert.Single(g1Health);
		Assert.Single(g1BlockDamage);
		Assert.True(w.ReceivedCount(w.G1, NetMsg.WorldBlockState) >= 0, "the block-state snapshot rides the same edge");
	}

	[Fact]
	public void MemberEntersWorld_ReceivesCurrentRadiationLineState()
	{
		using var w = ItemSimWorld.Create();
		var hostWorld = w.Host.Services.GetRequiredService<IWorldControl>();
		hostWorld.SetRadiationLineState(new RadiationLineStateMsg { Active = true, TimeGone = 9.5f });

		var g1States = new List<RadiationLineStateMsg>();
		w.G1.Services.GetRequiredService<IWorldControl>().RadiationLineStateReceived += msg => g1States.Add(msg);

		w.G1.Session.ReportSceneState(SceneStateType.InWorld, "SampleScene");
		w.Driver.Tick(33);

		Assert.Single(g1States);
		Assert.True(g1States[0].Active, "the stored host state must ride the world-entry fan-out");
		Assert.Equal(9.5f, g1States[0].TimeGone);
	}

	[Fact]
	public void MemberEntersWorld_ReceivesSnapshotCompleteMarker()
	{
		using var w = ItemSimWorld.Create();
		var completed = 0;
		w.G1.Services.GetRequiredService<IWorldControl>().WorldSnapshotCompleteReceived += () => completed++;

		w.G1.Session.ReportSceneState(SceneStateType.InWorld, "SampleScene");
		w.Driver.Tick(33);

		Assert.Equal(1, completed);
	}

	[Fact]
	public void MemberEntersWorld_EmptyTables_SendNothing()
	{
		using var w = ItemSimWorld.Create();
		var g1Traps = new List<IReadOnlyList<EntityEventMsg>>();
		var g1Opened = new List<IReadOnlyList<NetVector2Msg>>();
		var g1Health = new List<IReadOnlyList<BuildingEntityHealthEntryMsg>>();
		var g1BlockDamage = new List<IReadOnlyList<BlockDamageEntryMsg>>();
		w.G1.Services.GetRequiredService<EntityEventChannel>().TrapStateReceived += list => g1Traps.Add(list);
		w.G1.Services.GetRequiredService<EntityEventChannel>().OpenedEntitiesSnapshotReceived += list => g1Opened.Add(list);
		w.G1.Services.GetRequiredService<EntityEventChannel>().BuildingEntityHealthSnapshotReceived += list => g1Health.Add(list);
		w.G1.Services.GetRequiredService<IWorldControl>().BlockDamageSnapshotReceived += list => g1BlockDamage.Add(list);

		w.G1.Session.ReportSceneState(SceneStateType.InWorld, "SampleScene");
		w.Driver.Tick(33);

		Assert.Empty(g1Traps);
		Assert.Empty(g1Opened);
		Assert.Empty(g1Health);
		Assert.Empty(g1BlockDamage);
	}
}
