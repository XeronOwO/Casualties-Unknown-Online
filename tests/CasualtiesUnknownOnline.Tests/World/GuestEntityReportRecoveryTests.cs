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
/// Runtime-created world-entity recovery (sync-coverage audit E3). The creation
/// channel is one-shot: a swallowed report left a permanently one-sided entity
/// until a reconnect, because neither the world-entry group nor the 60 s cycle
/// carried a creation table. The host now keeps the accepted creations in
/// <see cref="RuntimeEntityRegistry"/> (absolute snapshot on world entry plus the
/// 60 s re-broadcast) and the guest keeps its unacknowledged reports in
/// <see cref="PendingEntityReportTable"/>, re-reported on the 60 s fallback
/// cycle until the host answers.
/// <para>
/// The host executor here is a contract double for the Game Adapter's thin
/// shell (the same pattern as <c>GuestBlockReportRecoveryTests</c>): it records
/// every report and relays it through the production host branch — the relay
/// includes the reporter, whose echo is its acknowledgement. The adapter's
/// materialization (and its 1 m dedup, covered purely by
/// <see cref="RuntimeEntityMatchTests"/>) needs a live Unity world and is
/// verified by the unified dual-client acceptance pass.
/// </para>
/// </summary>
[Trait("Category", "Integration")]
public class GuestEntityReportRecoveryTests
{
	private static EntitySpawnedMsg Creation(string id, float x, float y, ulong creator = 0, uint sequence = 0) => new()
	{
		Id = id,
		Position = new NetVector2Msg(x, y),
		Rotation = 0f,
		CreatorSteamId = creator,
		CreationSequence = sequence,
	};

	/// <summary>The host executor contract double: record the report and relay it to every member (the reporter included — its echo is the acknowledgement).</summary>
	private static List<(ulong Sender, EntitySpawnedMsg Msg)> InstallHostExecutor(ItemSimWorld w)
	{
		var reports = new List<(ulong Sender, EntitySpawnedMsg Msg)>();
		var hostWorld = w.Host.Services.GetRequiredService<IWorldControl>();
		hostWorld.EntitySpawnedReceived += (sender, msg) =>
		{
			reports.Add((sender, msg));
			hostWorld.SendEntitySpawned(msg); // accepted: relay to everyone, the reporter included
		};
		return reports;
	}

	private static int PendingCreations(TestNode guest) =>
		guest.Services.GetRequiredService<RuntimeEntityChannel>().PendingEntityReportCount;

	[Fact]
	public void SwallowedGuestCreationReport_IsReReportedOnTheFallbackCycleAndConverges()
	{
		using var w = ItemSimWorld.Create();
		var reports = InstallHostExecutor(w);
		var guestWorld = w.G1.Services.GetRequiredService<IWorldControl>();

		// The lazy-P2P window: the live creation report never lands.
		w.Driver.Network.SetFaults(w.G1.SteamId, w.Host.SteamId, new LinkFaults { Down = true });
		guestWorld.SendEntitySpawned(Creation("keypad", 12f, 34f));
		w.Driver.Tick(33);
		Assert.Empty(reports);

		// The link heals. Inside the 60 s window nothing is re-sent (the live
		// report just went out); past it the fallback re-reports the creation.
		w.Driver.Network.ClearFaults(w.G1.SteamId, w.Host.SteamId);
		w.Driver.Tick(59_000);
		Assert.Empty(reports);
		w.Driver.Tick(2_000);
		var report = Assert.Single(reports);
		Assert.Equal(w.G1.SteamId, report.Sender);
		Assert.Equal("keypad", report.Msg.Id);
		Assert.Equal(12f, report.Msg.Position.X);

		// The accepted relay reaches the third member, and the reporter's echo
		// clears the pending entry: no further re-report.
		Assert.True(w.ReceivedCount(w.G2, NetMsg.EntitySpawned) >= 1,
			$"the accepted relay must reach the other members, got {w.ReceivedCount(w.G2, NetMsg.EntitySpawned)}");
		w.Driver.Tick(61_000);
		Assert.Single(reports);
	}

	[Fact]
	public void SwallowedHostRelay_HostSnapshotReBroadcastConvergesTheMember()
	{
		using var w = ItemSimWorld.Create();
		var hostWorld = w.Host.Services.GetRequiredService<IWorldControl>();
		var g2Creations = new List<EntitySpawnedMsg>();
		w.G2.Services.GetRequiredService<IWorldControl>().EntitySpawnedReceived += (_, msg) => g2Creations.Add(msg);

		// The host creates its own entity; the live relay to G2 is swallowed.
		w.Driver.Network.SetFaults(w.Host.SteamId, w.G2.SteamId, new LinkFaults { Down = true });
		hostWorld.SendEntitySpawned(Creation("landmine", 7f, 8f));
		w.Driver.Tick(33);
		Assert.Empty(g2Creations);
		Assert.Equal(1, w.Host.Services.GetRequiredService<RuntimeEntityRegistry>().Count);

		// The 60 s cycle's absolute re-broadcast heals it without a reconnect.
		w.Driver.Network.ClearFaults(w.Host.SteamId, w.G2.SteamId);
		hostWorld.SendRuntimeEntitySnapshot(w.G2.SteamId);
		w.Driver.Tick(33);

		var creation = Assert.Single(g2Creations);
		Assert.Equal("landmine", creation.Id);
		Assert.Equal(1, w.ReceivedCount(w.G2, NetMsg.RuntimeEntitySnapshot));

		// Repeating the snapshot never grows the host's table (the receiver's
		// dedup is the adapter's 1 m match — RuntimeEntityMatchTests).
		hostWorld.SendRuntimeEntitySnapshot(w.G2.SteamId);
		w.Driver.Tick(33);
		Assert.Equal(2, g2Creations.Count);
		Assert.Equal(1, w.Host.Services.GetRequiredService<RuntimeEntityRegistry>().Count);
	}

	[Fact]
	public void LateJoiner_ReceivesTheAcceptedCreationTableOnWorldEntry()
	{
		using var w = ItemSimWorld.Create();
		var hostWorld = w.Host.Services.GetRequiredService<IWorldControl>();
		hostWorld.SendEntitySpawned(Creation("keypad", 1f, 1f));
		hostWorld.SendEntitySpawned(Creation("landmine", 2f, 2f));

		var g1Creations = new List<EntitySpawnedMsg>();
		w.G1.Services.GetRequiredService<IWorldControl>().EntitySpawnedReceived += (_, msg) => g1Creations.Add(msg);

		w.G1.Session.ReportSceneState(SceneStateType.InWorld, "SampleScene");
		w.Driver.Tick(33);

		Assert.Equal(2, g1Creations.Count);
		Assert.Contains(g1Creations, c => c.Id == "keypad");
		Assert.Contains(g1Creations, c => c.Id == "landmine");
		Assert.Equal(1, w.ReceivedCount(w.G1, NetMsg.RuntimeEntitySnapshot));
	}

	[Fact]
	public void HostSnapshot_AcknowledgesAPendingReportWhoseEchoWasSwallowed()
	{
		using var w = ItemSimWorld.Create();
		var reports = InstallHostExecutor(w);
		var guestWorld = w.G1.Services.GetRequiredService<IWorldControl>();

		// The report lands, but the host's echo back to the reporter is lost.
		w.Driver.Network.SetFaults(w.Host.SteamId, w.G1.SteamId, new LinkFaults { Down = true });
		guestWorld.SendEntitySpawned(Creation("keypad", 3f, 4f));
		w.Driver.Tick(33);
		Assert.Single(reports);
		Assert.Equal(1, PendingCreations(w.G1));

		// The host's absolute table arrives: it is the acknowledgement too.
		w.Driver.Network.ClearFaults(w.Host.SteamId, w.G1.SteamId);
		w.Host.Services.GetRequiredService<IWorldControl>().SendRuntimeEntitySnapshot(w.G1.SteamId);
		w.Driver.Tick(33);
		Assert.Equal(0, PendingCreations(w.G1));

		w.Driver.Tick(61_000);
		Assert.Single(reports);
	}

	[Fact]
	public void EntityDestroyedBeforeTheAnswer_HostDropsTheAcceptedRecord()
	{
		using var w = ItemSimWorld.Create();
		var hostWorld = w.Host.Services.GetRequiredService<IWorldControl>();
		hostWorld.SendEntitySpawned(Creation("keypad", 5f, 6f));
		var registry = w.Host.Services.GetRequiredService<RuntimeEntityRegistry>();
		Assert.Equal(1, registry.Count);

		hostWorld.ReportRuntimeEntityDestroyed(new RuntimeEntityKey("keypad", 5, 6, 0, 0));

		Assert.Equal(0, registry.Count);
		Assert.Empty(registry.Entries);
	}

	[Fact]
	public void EntityDestroyedBeforeTheAnswer_GuestDropsThePendingReport()
	{
		using var w = ItemSimWorld.Create();
		var reports = InstallHostExecutor(w);
		var guestWorld = w.G1.Services.GetRequiredService<IWorldControl>();

		w.Driver.Network.SetFaults(w.G1.SteamId, w.Host.SteamId, new LinkFaults { Down = true });
		guestWorld.SendEntitySpawned(Creation("keypad", 8f, 9f));
		w.Driver.Tick(33);
		Assert.Equal(1, PendingCreations(w.G1));

		// The guest's own copy died before the host answered — a dead entity
		// must never be re-reported (and so never resurrected).
		guestWorld.ReportRuntimeEntityDestroyed(new RuntimeEntityKey("keypad", 8, 9, 0, 0));
		Assert.Equal(0, PendingCreations(w.G1));

		w.Driver.Network.ClearFaults(w.G1.SteamId, w.Host.SteamId);
		w.Driver.Tick(61_000);
		Assert.Empty(reports);
	}

	[Fact]
	public void TwoCreationsInOneCell_OneDies_OnlyItsOwnRecordIsDropped()
	{
		using var w = ItemSimWorld.Create();
		var hostWorld = w.Host.Services.GetRequiredService<IWorldControl>();
		var registry = w.Host.Services.GetRequiredService<RuntimeEntityRegistry>();
		var survivor = new RuntimeEntityKey("turret", 5, 7, w.G1.SteamId, 1);
		var dead = new RuntimeEntityKey("turret", 5, 7, w.G1.SteamId, 2);

		hostWorld.SendEntitySpawned(Creation("turret", 5.2f, 7.2f, w.G1.SteamId, 1));
		hostWorld.SendEntitySpawned(Creation("turret", 5.9f, 7.6f, w.G1.SteamId, 2));
		Assert.Equal(2, registry.Count);

		// The death hook reports the stamped CREATION key: the sibling's record
		// must survive, or the 60 s re-broadcast would resurrect the dead one
		// and stop healing the live one.
		hostWorld.ReportRuntimeEntityDestroyed(dead);

		Assert.Equal(1, registry.Count);
		Assert.Equal(survivor, RuntimeEntityKey.From(Assert.Single(registry.Entries)));
	}

	[Fact]
	public void WorldSnapshotComplete_DoesNotDropUnansweredCreations()
	{
		using var w = ItemSimWorld.Create();
		var reports = InstallHostExecutor(w);
		var guestWorld = w.G1.Services.GetRequiredService<IWorldControl>();

		w.Driver.Network.SetFaults(w.G1.SteamId, w.Host.SteamId, new LinkFaults { Down = true });
		guestWorld.SendEntitySpawned(Creation("keypad", 5f, 7f));
		w.Driver.Tick(33);

		// The world-entry completion marker is NOT a world boundary: a
		// reconnect-while-in-world keeps the guest's local creations, so an
		// unanswered report must stay pending and be re-reported.
		w.Host.Services.GetRequiredService<IWorldControl>().SendWorldSnapshotComplete(w.G1.SteamId);
		w.Driver.Tick(33);

		w.Driver.Network.ClearFaults(w.G1.SteamId, w.Host.SteamId);
		w.Driver.Tick(61_000);
		Assert.Single(reports);
	}

	[Fact]
	public void LayerReset_DropsThePreviousWorldsPendingCreations()
	{
		using var w = ItemSimWorld.Create();
		var reports = InstallHostExecutor(w);
		var guestWorld = w.G1.Services.GetRequiredService<IWorldControl>();

		w.Driver.Network.SetFaults(w.G1.SteamId, w.Host.SteamId, new LinkFaults { Down = true });
		guestWorld.SendEntitySpawned(Creation("keypad", 5f, 7f));
		w.Driver.Tick(33);

		// The guest applied a new world/layer baseline (the adapter's
		// WorldParamsService calls this at the generation boundary): the
		// previous world's creations must not be materialized into the new one.
		guestWorld.ResetPendingEntityReports();
		w.Driver.Network.ClearFaults(w.G1.SteamId, w.Host.SteamId);
		w.Driver.Tick(61_000);

		Assert.Equal(0, PendingCreations(w.G1));
		Assert.Empty(reports);
	}

	[Fact]
	public void SessionEnd_ClearsPendingCreations()
	{
		using var w = ItemSimWorld.Create();
		_ = InstallHostExecutor(w);
		var guestWorld = w.G1.Services.GetRequiredService<IWorldControl>();
		w.Driver.Network.SetFaults(w.G1.SteamId, w.Host.SteamId, new LinkFaults { Down = true });
		guestWorld.SendEntitySpawned(Creation("keypad", 5f, 7f));
		w.Driver.Tick(33);
		Assert.Equal(1, PendingCreations(w.G1));

		w.G1.Session.EndSession();

		Assert.Equal(0, PendingCreations(w.G1));
	}

	[Fact]
	public void AnimalCreation_IsRecoveredThroughTheGuestPendingTableButNeverEntersTheHostAcceptedTable()
	{
		using var w = ItemSimWorld.Create();
		var reports = InstallHostExecutor(w);
		var guestWorld = w.G1.Services.GetRequiredService<IWorldControl>();

		// An animal travels the live channel (the immediate peer copy is what
		// the enemy binding pairs with). Its HOST record stays empty: the
		// enemy snapshot owns the late-join copy at the animal's CURRENT
		// position, and a host record would make a late joiner materialize a
		// second copy at the creation position. The GUEST pending table still
		// records it — a swallowed guest → host animal report has no other
		// in-session recovery.
		var animal = Creation("crystalenemy", 4f, 5f);
		animal.IsAnimal = true;
		w.Driver.Network.SetFaults(w.Host.SteamId, w.G1.SteamId, new LinkFaults { Down = true }); // the echo is swallowed
		guestWorld.SendEntitySpawned(animal);
		w.Driver.Tick(33);

		Assert.Single(reports);
		Assert.Equal(0, w.Host.Services.GetRequiredService<RuntimeEntityRegistry>().Count);
		Assert.Equal(1, PendingCreations(w.G1));

		// The link heals: the fallback re-reports, the host's echo clears it.
		w.Driver.Network.ClearFaults(w.Host.SteamId, w.G1.SteamId);
		w.Driver.Tick(61_000);

		Assert.Equal(2, reports.Count);
		Assert.Equal(0, PendingCreations(w.G1));
	}

	[Fact]
	public void DuplicateCreationReport_KeepsOneAcceptedRecord()
	{
		using var w = ItemSimWorld.Create();
		var reports = InstallHostExecutor(w);
		var guestWorld = w.G1.Services.GetRequiredService<IWorldControl>();

		guestWorld.SendEntitySpawned(Creation("keypad", 5f, 7f));
		guestWorld.SendEntitySpawned(Creation("keypad", 5f, 7f)); // a repeat of the same creation
		w.Driver.Tick(33);

		Assert.Equal(2, reports.Count);
		Assert.Equal(1, w.Host.Services.GetRequiredService<RuntimeEntityRegistry>().Count);
		Assert.Equal(0, PendingCreations(w.G1));
	}

	[Fact]
	public void TwoCreationsOfTheSamePrefabInOneCell_AreTwoAcceptedRecords()
	{
		using var w = ItemSimWorld.Create();
		var reports = InstallHostExecutor(w);
		var guestWorld = w.G1.Services.GetRequiredService<IWorldControl>();

		// Two turrets 0.7 m apart: the SAME floored cell (5, 7) but two distinct
		// creations. The creating side's monotonic token is the only thing that
		// can tell them apart — a cell-keyed record would swallow the second
		// one and the peer would never materialize it.
		guestWorld.SendEntitySpawned(Creation("turret", 5.2f, 7.2f, w.G1.SteamId, 1));
		guestWorld.SendEntitySpawned(Creation("turret", 5.9f, 7.6f, w.G1.SteamId, 2));
		w.Driver.Tick(33);

		Assert.Equal(2, reports.Count);
		Assert.Equal(2, w.Host.Services.GetRequiredService<RuntimeEntityRegistry>().Count);
		Assert.Equal(0, PendingCreations(w.G1)); // both echoes acknowledged their own report
	}

	[Fact]
	public void AnimalCreation_IsAcknowledgedByTheHostSnapshot()
	{
		using var w = ItemSimWorld.Create();
		var reports = InstallHostExecutor(w);
		var guestWorld = w.G1.Services.GetRequiredService<IWorldControl>();

		// An accepted animal never enters the host's materializable table (the
		// enemy domain owns its late-join copy), so the entry list alone can
		// never acknowledge the report. The host snapshot must still answer it,
		// or a lost echo leaves the guest re-reporting until the animal dies.
		var animal = Creation("crystalenemy", 4f, 5f, w.G1.SteamId, 3);
		animal.IsAnimal = true;
		w.Driver.Network.SetFaults(w.Host.SteamId, w.G1.SteamId, new LinkFaults { Down = true }); // the echo is swallowed
		guestWorld.SendEntitySpawned(animal);
		w.Driver.Tick(33);
		Assert.Single(reports);
		Assert.Equal(1, PendingCreations(w.G1));

		var g1Creations = new List<EntitySpawnedMsg>();
		guestWorld.EntitySpawnedReceived += (_, msg) => g1Creations.Add(msg);
		w.Driver.Network.ClearFaults(w.Host.SteamId, w.G1.SteamId);
		w.Host.Services.GetRequiredService<IWorldControl>().SendRuntimeEntitySnapshot(w.G1.SteamId);
		w.Driver.Tick(33);

		Assert.Equal(1, w.ReceivedCount(w.G1, NetMsg.RuntimeEntitySnapshot));
		Assert.Equal(0, PendingCreations(w.G1));
		Assert.Empty(g1Creations); // the acknowledgement never materializes a copy
	}

	[Fact]
	public void UnmaterializableCreation_IsRelayedAndAcknowledgedWithoutBeingRecorded()
	{
		using var w = ItemSimWorld.Create();
		var hostWorld = w.Host.Services.GetRequiredService<IWorldControl>();
		var g2Creations = new List<EntitySpawnedMsg>();
		w.G2.Services.GetRequiredService<IWorldControl>().EntitySpawnedReceived += (_, msg) => g2Creations.Add(msg);
		var guestWorld = w.G1.Services.GetRequiredService<IWorldControl>();

		// The adapter's contract double for the host-side failure branch: the
		// host lacks the prefab and cannot materialize its own copy. Accept-first
		// still requires the creation to reach a member that HAS the prefab, and
		// the reporter's echo to acknowledge its report.
		hostWorld.EntitySpawnedReceived += (sender, msg) => hostWorld.ReportEntitySpawnUnmaterialized(sender, msg);

		guestWorld.SendEntitySpawned(Creation("modcrate", 6f, 6f, w.G1.SteamId, 4));
		w.Driver.Tick(33);

		var relayed = Assert.Single(g2Creations);
		Assert.Equal("modcrate", relayed.Id);
		Assert.Equal(0, PendingCreations(w.G1));

		// NOT recorded: the host has no local copy whose death could drop the
		// record, so a record would re-materialize the creation on a member that
		// later destroyed its copy (the resurrection this mechanism prevents).
		Assert.Equal(0, w.Host.Services.GetRequiredService<RuntimeEntityRegistry>().Count);
		w.Host.Services.GetRequiredService<IWorldControl>().SendRuntimeEntitySnapshot(w.G2.SteamId);
		w.Driver.Tick(33);
		Assert.Single(g2Creations); // the snapshot carries no record for it
	}

	[Fact]
	public void UnmaterializableReport_OnAGuest_IsNotRelayed()
	{
		using var w = ItemSimWorld.Create();
		var hostCreations = new List<EntitySpawnedMsg>();
		w.Host.Services.GetRequiredService<IWorldControl>().EntitySpawnedReceived += (_, msg) => hostCreations.Add(msg);
		var guestWorld = w.G1.Services.GetRequiredService<IWorldControl>();

		// Only the host relays a creation it could not materialize; a guest that
		// hits the same branch must stay silent (the guard is what keeps a guest
		// from broadcasting on the host's behalf).
		guestWorld.ReportEntitySpawnUnmaterialized(w.Host.SteamId, Creation("modcrate", 6f, 6f, w.Host.SteamId, 5));
		w.Driver.Tick(33);

		Assert.Empty(hostCreations);
	}
}
