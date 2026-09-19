using System.Linq;
using CasualtiesUnknownOnline.Protocol.Wire;
using CasualtiesUnknownOnline.Runtime.Protocol;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using CasualtiesUnknownOnline.Runtime.Session.Items;
using CasualtiesUnknownOnline.Tests.Fakes;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Items;

/// <summary>
/// Guest item-command convergence (sync-coverage audit row I5). An item command is
/// a report of a native action that already happened on the guest, and the
/// transport only retries while the peer is reachable: the documented lazy-P2P
/// swallow window drops frames the sender never learns about, so a swallowed
/// command used to leave the two sides disagreeing about the item for the rest of
/// the run (the guest held it, the host's table still had it in the world, or the
/// reverse) — I3's world-item keyframe converges the HOST's table, never the
/// guest's local result.
/// <para>
/// The fix under test: the guest keeps each unacknowledged command as a whole frame
/// and re-sends that exact frame in a bounded 5 s x 12 window, so the host's kernel
/// answers a repeat from its own operation window (same <c>OperationId</c>, one
/// commit) and a report leaves the window on either verdict the wire carries — its
/// own id in a committed batch, or a refusal naming the item. The reports are kept
/// PER ITEM AND IN SEND ORDER, which is the mechanism: one production frame reports
/// the creation and the pickup that follows it (the adapter's generation-time item
/// report), a lost creation is the one thing no later report can repair, and a
/// pickup behind a lost drop is refused as a conflict — so the chain is replayed in
/// order and a refusal drops only its NEWEST report. Every swallow scenario drops
/// the guest's <c>KernelEnvelope</c> channel on a link that still reports success,
/// which is the production swallow contract.
/// </para>
/// </summary>
[Trait("Category", "Integration")]
public class GuestCommandReconciliationTests
{
	private static CharacterItemMsg Item(float condition = 1f) => new()
	{
		ItemId = "test_item",
		Condition = condition,
		Contents = [],
	};

	/// <summary>Swallow the guest's kernel channel towards the host — every item command (and stream frame) on it is lost while the link reports success.</summary>
	private static void SwallowKernelChannel(ItemSimWorld w, TestNode from, TestNode to) =>
		w.Driver.Network.SetFaults(from.SteamId, to.SteamId, new LinkFaults { DropMessageId = NetMsg.KernelEnvelope });

	private static void HealKernelChannel(ItemSimWorld w, TestNode from, TestNode to) =>
		w.Driver.Network.ClearFaults(from.SteamId, to.SteamId);

	/// <summary>The unacknowledged-report window of one node.</summary>
	private static GuestCommandReconciliation Window(TestNode node) =>
		node.Services.GetRequiredService<GuestCommandReconciliation>();

	private static ulong HostRevision(ItemSimWorld w) =>
		w.Host.Services.GetRequiredService<ItemKernelAuthority>().CurrentGlobalRevision;

	/// <summary>A live count of the kernel frames the HOST receives from one sender.</summary>
	private sealed class HostKernelFrames(TestNode host, ulong from)
	{
		private int _count;

		internal void Bind() => host.Transport.MessageReceived += (sender, frame) =>
		{
			if ((NetMsg)frame[0] == NetMsg.KernelEnvelope && sender == from)
			{
				_count++;
			}
		};

		internal int Count => _count;
	}

	/// <summary>Setup: G1 reports an item's creation and the host holds it in its world table.</summary>
	private static void SpawnWorldItem(ItemSimWorld w, ulong itemId)
	{
		w.Spawn(w.G1, itemId, Item());
		w.Driver.Tick(100);
		Assert.True(w.HostTable(itemId), "setup: the host must hold the reported item");
	}

	[Fact]
	public void SwallowedPickup_ConvergesThroughTheReReportWindow()
	{
		using var w = ItemSimWorld.Create();
		SpawnWorldItem(w, 42);

		SwallowKernelChannel(w, w.G1, w.Host);
		w.Pickup(w.G1, 42, Item());
		w.Driver.Tick(500);

		Assert.True(w.HostTable(42), "the swallowed claim must leave the host's table holding the item");
		Assert.Empty(w.CarriedEvents(w.G1));
		Assert.Equal(1, Window(w.G1).PendingCount); // the window is open on the unacknowledged command

		HealKernelChannel(w, w.G1, w.Host);
		w.Driver.TickUntil(() => !w.HostTable(42), maxMs: 30_000);

		Assert.False(w.HostTable(42), "the re-report must teach the host that the item left the world");
		Assert.True(w.TransferredOf(w.G1, 42), "the host must register the pickup in the guest's transfer table");
		Assert.Contains(w.CarriedEvents(w.G1), c => c.Item.InstanceId == 42 && c.Owner == w.G1.SteamId);
		Assert.Equal(0, Window(w.G1).PendingCount); // the committed batch closed the window
	}

	[Fact]
	public void SwallowedDrop_ConvergesThroughTheReReportWindow()
	{
		using var w = ItemSimWorld.Create();
		SpawnWorldItem(w, 42);
		w.Pickup(w.G1, 42, Item());
		w.Driver.Tick(100);
		Assert.True(w.TransferredOf(w.G1, 42), "setup: the guest holds the item");

		SwallowKernelChannel(w, w.G1, w.Host);
		w.Drop(w.G1, 42, Item());
		w.Driver.Tick(500);

		Assert.True(w.TransferredOf(w.G1, 42), "the swallowed drop must leave the host holding the guest's carry");
		Assert.Equal(1, Window(w.G1).PendingCount);

		HealKernelChannel(w, w.G1, w.Host);
		w.Driver.TickUntil(() => !w.TransferredOf(w.G1, 42), maxMs: 30_000);

		Assert.True(w.HostTable(42), "the re-report must put the item back in the host's world table");
		Assert.Equal(0, Window(w.G1).PendingCount);
	}

	[Fact]
	public void SwallowedDestroy_ConvergesThroughTheReReportWindow()
	{
		using var w = ItemSimWorld.Create();
		SpawnWorldItem(w, 42);

		SwallowKernelChannel(w, w.G1, w.Host);
		w.Destroy(w.G1, 42);
		w.Driver.Tick(500);

		Assert.True(w.HostTable(42), "the swallowed destroy must leave the host's table untouched");
		Assert.Equal(1, Window(w.G1).PendingCount);

		HealKernelChannel(w, w.G1, w.Host);
		w.Driver.TickUntil(() => !w.HostTable(42), maxMs: 30_000);

		Assert.False(w.HostTable(42), "the re-report must teach the host that the item is gone");
		Assert.Equal(0, Window(w.G1).PendingCount);
	}

	[Fact]
	public void SwallowedDestroyOfTheLastCarriedItem_ConvergesWithAnEmptyWorldTable()
	{
		// The empty-host-table case the audit's I1 caveat names: the world-item
		// keyframe is skipped while the host's table is empty, so a swallowed destroy
		// of the guest's carried item had no heal at all — nothing but the command
		// re-report can remove the host's transfer entry.
		using var w = ItemSimWorld.Create();
		SpawnWorldItem(w, 42);
		w.Pickup(w.G1, 42, Item());
		w.Driver.Tick(100);
		Assert.False(w.HostTable(42), "setup: the item is the guest's carry, not a world item");

		SwallowKernelChannel(w, w.G1, w.Host);
		w.Destroy(w.G1, 42);
		w.Driver.Tick(500);
		Assert.True(w.TransferredOf(w.G1, 42), "the swallowed destroy must leave the transfer entry in place");

		HealKernelChannel(w, w.G1, w.Host);
		w.Driver.TickUntil(() => !w.TransferredOf(w.G1, 42), maxMs: 30_000);

		Assert.False(w.TransferredOf(w.G1, 42), "the re-report must remove the transfer entry");
		Assert.Equal(0, Window(w.G1).PendingCount);
	}

	[Fact]
	public void SwallowedCreation_IsStillQueuedAndReReportedAfterALaterPickupIsRefused()
	{
		// The independent review's MAJOR-1, in the arrangement where the creation's report
		// is lost and a later pickup IS delivered: a window that kept only the newest
		// report per item would have evicted the creation, and a lost creation is the one
		// report no later one can stand in for — the host answers every operation on that
		// item with "creation this host has never judged" and rolls the guest's pickup
		// back, with nothing left to re-report. (The back-to-back pair the adapter's
		// generation-time report sends — PickupSync's lambda — is the next test's shape.)
		using var w = ItemSimWorld.Create();

		SwallowKernelChannel(w, w.G1, w.Host);
		w.Spawn(w.G1, 42, Item()); // the creation is swallowed
		HealKernelChannel(w, w.G1, w.Host);
		w.Pickup(w.G1, 42, Item()); // the operation is delivered and refused
		w.Driver.Tick(200);

		Assert.Single(w.Rejects(w.G1));
		Assert.Equal([WireCommandKind.ItemSpawn], Window(w.G1).PendingKindsFor(42));
		Assert.False(w.HostTable(42));

		w.Driver.TickUntil(() => w.HostTable(42), maxMs: 30_000);

		Assert.True(w.HostTable(42), "the creation's re-report must materialize the item on the host");
		Assert.Single(w.Rejects(w.G1)); // …and the item is usable again: the refusal is not repeated
		Assert.Equal(0, Window(w.G1).PendingCount);
	}

	[Fact]
	public void BothReportsSwallowed_TheChainReplaysInSendOrderAndKeepsThePlayersCarry()
	{
		// Creation first, then the operation: the order is the mechanism. Replaying the
		// pickup first would have it refused (no judged creation) and end with the item
		// on the ground; replaying the report chain in its original order keeps the
		// player's action — the item ends up where they put it.
		using var w = ItemSimWorld.Create();

		SwallowKernelChannel(w, w.G1, w.Host);
		w.Spawn(w.G1, 42, Item());
		w.Pickup(w.G1, 42, Item());
		w.Driver.Tick(200);

		Assert.Equal([WireCommandKind.ItemSpawn, WireCommandKind.ItemPickup], Window(w.G1).PendingKindsFor(42));
		Assert.False(w.HostTable(42));
		Assert.Empty(w.Rejects(w.G1));

		HealKernelChannel(w, w.G1, w.Host);
		w.Driver.TickUntil(() => w.TransferredOf(w.G1, 42), maxMs: 30_000);

		Assert.False(w.HostTable(42), "the creation was judged before the pickup — the item ends up carried, as the player left it");
		Assert.Empty(w.Rejects(w.G1));
		Assert.Equal(0, Window(w.G1).PendingCount);
	}

	[Fact]
	public void SwallowedDropBehindAPickup_ConvergesThroughTheOlderReport()
	{
		// The same supersede hazard one level down: with only the newest report kept,
		// a LOST drop followed by a pickup left the host holding the older location and
		// refused the pickup as a conflict — divergence in either direction. The older
		// report stays queued, so after the refusal the drop is re-reported and both
		// sides agree the item is back in the world (the guest's local half is the
		// adapter's own rollback, which this suite does not simulate).
		using var w = ItemSimWorld.Create();
		SpawnWorldItem(w, 42);
		w.Pickup(w.G1, 42, Item());
		w.Driver.Tick(100);
		Assert.True(w.TransferredOf(w.G1, 42), "setup: the guest holds the item");

		SwallowKernelChannel(w, w.G1, w.Host);
		w.Drop(w.G1, 42, Item()); // lost
		HealKernelChannel(w, w.G1, w.Host);
		w.Pickup(w.G1, 42, Item()); // refused: the host still holds the guest's carry
		w.Driver.Tick(200);

		Assert.Single(w.Rejects(w.G1));
		Assert.Equal([WireCommandKind.ItemDrop], Window(w.G1).PendingKindsFor(42));

		w.Driver.TickUntil(() => w.HostTable(42), maxMs: 30_000);

		Assert.False(w.TransferredOf(w.G1, 42), "the older drop's re-report must land — the host converges to the state the refusal left behind");
		Assert.Single(w.Rejects(w.G1));
		Assert.Equal(0, Window(w.G1).PendingCount);
	}

	[Fact]
	public void RepeatOfAJudgedCommand_IsAnsweredFromTheOperationWindowAndCommitsOnce()
	{
		// The verdict half of the round trip can be lost too: the guest re-reports a
		// command the host has already committed, and the host's kernel answers the
		// repeat with the ORIGINAL decision (one operation id, one commit) and
		// re-broadcasts that same batch.
		using var w = ItemSimWorld.Create();
		SpawnWorldItem(w, 42);
		var before = HostRevision(w);
		var received = new HostKernelFrames(w.Host, w.G1.SteamId);
		received.Bind();

		SwallowKernelChannel(w, w.Host, w.G1); // the committed batch never reaches the guest
		w.Pickup(w.G1, 42, Item());
		w.Driver.Tick(1_000);

		w.Driver.TickUntil(() => Window(w.G1).PendingCount > 0, maxMs: 10_000);
		Assert.Equal(before + 1, HostRevision(w)); // the host judged the command exactly once

		// The re-report goes out while the guest is still blind to the verdict, so the
		// host judges the repeat before the link heals.
		for (var i = 0; i < 400; i++)
		{
			w.Driver.Tick(33); // ~13 s — two more windows elapse
		}

		Assert.Equal(before + 1, HostRevision(w)); // a repeat never commits a second operation
		Assert.True(received.Count >= 3, $"the guest must have re-reported the judged command, but the host saw only {received.Count} kernel frame(s)");
		Assert.True(w.TransferredOf(w.G1, 42));

		HealKernelChannel(w, w.Host, w.G1);
		w.Driver.TickUntil(() => Window(w.G1).PendingCount == 0, maxMs: 30_000);
		Assert.Equal(before + 1, HostRevision(w));
	}

	[Fact]
	public void DuplicateWireDelivery_IsAbsorbedByTheOperationWindow()
	{
		// Row 5 of the ticket's matrix at the WIRE level: the same frame delivered twice
		// (a retransmission) is ONE operation — the kernel answers the second delivery
		// from its operation window, so the creation and the pickup each commit exactly
		// once and the guest converges with an empty window. (This is a guard for wire
		// duplication, not a regression test for the per-item queue: one report per item
		// is in flight here, which the previous single-slot window also handled.)
		using var w = ItemSimWorld.Create();
		var before = HostRevision(w);
		w.Driver.Network.SetFaults(w.G1.SteamId, w.Host.SteamId, new LinkFaults { Duplicate = true });

		w.Spawn(w.G1, 42, Item());
		w.Pickup(w.G1, 42, Item());
		w.Driver.Tick(200);

		Assert.Equal(before + 2, HostRevision(w)); // the creation and the pickup, each judged exactly once
		Assert.False(w.HostTable(42));
		Assert.True(w.TransferredOf(w.G1, 42));
		Assert.Empty(w.Rejects(w.G1));
		Assert.Equal(0, Window(w.G1).PendingCount);
	}

	[Fact]
	public void SpentWindow_StopsReReportingAndNamesTheLoss()
	{
		// The window is bounded: a command the host never receives is NAMED (a warning
		// carrying the report's identity) and the re-reports stop instead of trickling
		// for the rest of the session.
		var recorder = new RecordingLoggerFactory();
		using var w = ItemSimWorld.Create(s => s.AddSingleton<ILoggerFactory>(recorder));
		SpawnWorldItem(w, 42);

		SwallowKernelChannel(w, w.G1, w.Host);
		w.Pickup(w.G1, 42, Item());
		w.Driver.Tick(1_000);
		Assert.Equal(1, Window(w.G1).PendingCount);

		var before = HostRevision(w);
		w.Driver.TickUntil(() => Window(w.G1).PendingCount == 0, maxMs: 120_000);
		Assert.Equal(0, Window(w.G1).PendingCount);

		HealKernelChannel(w, w.G1, w.Host);
		for (var i = 0; i < 900; i++)
		{
			w.Driver.Tick(33); // 30 s — six more windows had the budget not been spent
		}

		Assert.Equal(0, Window(w.G1).PendingCount);
		Assert.Equal(before, HostRevision(w)); // the command never reached the host
		Assert.True(w.HostTable(42), "the host keeps its own copy of the item — the accepted loss");
		var losses = recorder.Messages(LogLevel.Warning, nameof(GuestCommandReconciliation));
		Assert.True(
			losses.Any(line => line.Contains("was not answered by the host after 12 re-report(s)")),
			"the spent window must name its loss in a warning (its own surfacing path)");
	}

	[Fact]
	public void RefusedCommand_ClosesTheWindowInsteadOfTrickling()
	{
		// A refusal is a verdict too: the host answers an operation whose creation it
		// never judged at once, and the window must stop re-reporting it.
		using var w = ItemSimWorld.Create();
		var received = new HostKernelFrames(w.Host, w.G1.SteamId);
		received.Bind();

		w.Pickup(w.G1, 9999); // an item the host never judged the creation of
		w.Driver.Tick(500);

		Assert.Single(w.Rejects(w.G1));
		Assert.Equal(0, Window(w.G1).PendingCount);

		for (var i = 0; i < 600; i++)
		{
			w.Driver.Tick(33); // 20 s — four windows had the refusal not closed it
		}

		Assert.Single(w.Rejects(w.G1));
		Assert.Equal(1, received.Count); // the live report, and not one re-report
	}

	[Fact]
	public void ThirdParty_SeesTheHealedLocationAndNoDuplicate()
	{
		// Row 7 of the ticket's matrix: a repeat report is answered to the run, not to
		// the reporter — the third member's view converges with everyone else's.
		using var w = ItemSimWorld.Create();
		SpawnWorldItem(w, 42);
		var g2Spawns = w.SpawnedEvents(w.G2);

		SwallowKernelChannel(w, w.G1, w.Host);
		w.Pickup(w.G1, 42, Item());
		w.Driver.Tick(500);
		HealKernelChannel(w, w.G1, w.Host);
		w.Driver.TickUntil(() => !w.HostTable(42), maxMs: 30_000);

		Assert.Contains(w.CarriedEvents(w.G2), c => c.Item.InstanceId == 42 && c.Owner == w.G1.SteamId);
		Assert.Equal(g2Spawns, w.SpawnedEvents(w.G2)); // no duplicate materialization on the third party
	}

	[Fact]
	public void SessionEnd_DropsTheUnacknowledgedCommands()
	{
		// A window that survived the session would re-report the previous run's
		// operation into the next one; the session edge clears it, and the rejoin
		// re-baselines the world from the host's checkpoint and item snapshot instead.
		using var w = ItemSimWorld.Create();
		SpawnWorldItem(w, 42);

		SwallowKernelChannel(w, w.G1, w.Host);
		w.Pickup(w.G1, 42, Item());
		w.Driver.Tick(500);
		Assert.Equal(1, Window(w.G1).PendingCount);

		w.G1.Session.EndSession();

		Assert.Equal(0, Window(w.G1).PendingCount);
	}
}
