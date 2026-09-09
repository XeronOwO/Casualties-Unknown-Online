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
	private static EntitySpawnedMsg Creation(string id, float x, float y) => new()
	{
		Id = id,
		Position = new NetVector2Msg(x, y),
		Rotation = 0f,
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

		hostWorld.ReportRuntimeEntityDestroyed("keypad", 5f, 6f);

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
		guestWorld.ReportRuntimeEntityDestroyed("keypad", 8f, 9f);
		Assert.Equal(0, PendingCreations(w.G1));

		w.Driver.Network.ClearFaults(w.G1.SteamId, w.Host.SteamId);
		w.Driver.Tick(61_000);
		Assert.Empty(reports);
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
}
