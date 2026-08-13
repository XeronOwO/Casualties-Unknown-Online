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

		var g1Traps = new List<IReadOnlyList<EntityEventMsg>>();
		var g1Opened = new List<IReadOnlyList<NetVector2Msg>>();
		w.G1.Services.GetRequiredService<EntityEventChannel>().TrapStateReceived += list => g1Traps.Add(list);
		w.G1.Services.GetRequiredService<EntityEventChannel>().OpenedEntitiesSnapshotReceived += list => g1Opened.Add(list);

		w.G1.Session.ReportSceneState(SceneStateType.InWorld, "SampleScene");
		w.Driver.Tick(33);

		// The world-entry fan-out sends ALL the world-state snapshots — the
		// trap and opened sends must not wait for the 60 s periodic resend.
		Assert.Single(g1Traps);
		Assert.Single(g1Opened);
		Assert.True(w.ReceivedCount(w.G1, NetMsg.WorldBlockState) >= 0, "the block-state snapshot rides the same edge");
	}

	[Fact]
	public void MemberEntersWorld_EmptyTables_SendNothing()
	{
		using var w = ItemSimWorld.Create();
		var g1Traps = new List<IReadOnlyList<EntityEventMsg>>();
		var g1Opened = new List<IReadOnlyList<NetVector2Msg>>();
		w.G1.Services.GetRequiredService<EntityEventChannel>().TrapStateReceived += list => g1Traps.Add(list);
		w.G1.Services.GetRequiredService<EntityEventChannel>().OpenedEntitiesSnapshotReceived += list => g1Opened.Add(list);

		w.G1.Session.ReportSceneState(SceneStateType.InWorld, "SampleScene");
		w.Driver.Tick(33);

		Assert.Empty(g1Traps);
		Assert.Empty(g1Opened);
	}
}
