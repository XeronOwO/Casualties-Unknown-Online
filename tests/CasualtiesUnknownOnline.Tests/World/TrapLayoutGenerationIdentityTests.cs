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
/// The trap-layout family's world/layer generation identity
/// (review/generation-identity-remaining-families): the entries are positions of
/// GENERATED entities, so a repair derived in one layer that reaches a guest
/// already in the next one materialized the previous layer's traps into the new
/// world. The host stamps every send with its kernel run baseline and the
/// receiving seam refuses a STALE snapshot whole; an UNKNOWN stamp (no committed
/// baseline on one side) keeps the pre-stamp behaviour.
/// <para>
/// What the send path attaches is proven by the frame-level test (the stamp is
/// decoded off the wire, never passed by the caller); the cross-layer ARRIVALS
/// are reproduced with an explicit stamp, because this harness propagates the
/// host's run baseline to the guests and so cannot hold the two sides at
/// different layers locally — the same split the block-report family's suite
/// uses. The adapter's materialization half (Unity prefabs) stays covered by the
/// unified dual-client acceptance pass.
/// </para>
/// </summary>
[Trait("Category", "Integration")]
public class TrapLayoutGenerationIdentityTests
{
	private const string Prefab = "spikestabber";

	private static void HostReportsOneTrap(ItemSimWorld w) =>
		w.Host.Services.GetRequiredService<IWorldControl>().ReportTrapLayout(EntityEventKind.SpikeStabbed, -13f, 466.8f, Prefab);

	private static List<IReadOnlyList<TrapLayoutEntryMsg>> Listen(TestNode guest)
	{
		var received = new List<IReadOnlyList<TrapLayoutEntryMsg>>();
		guest.Services.GetRequiredService<IWorldControl>().TrapLayoutReceived += received.Add;
		return received;
	}

	/// <summary>Send one layout snapshot to a member with an explicit generation stamp — how a repair that crossed a layer boundary arrives.</summary>
	private static void SendStampedSnapshot(ItemSimWorld w, TestNode target, WorldGenerationMsg? generation)
	{
		List<TrapLayoutEntryMsg> entries =
		[
			new() { Kind = EntityEventKind.SpikeStabbed, X = -13f, Y = 466.8f, PrefabName = Prefab },
		];
		w.Host.Services.GetRequiredService<PacketSender>().Send(
			target.SteamId,
			NetMsg.TrapLayoutSnapshot,
			new TrapLayoutSnapshotMsg { Entries = entries, Generation = generation });
	}

	[Fact]
	public void SnapshotFromAPreviousLayer_IsRefusedWhole_AndNothingIsMaterialized()
	{
		using var w = ItemSimWorld.Create();
		WorldGenerationReports.CommitRun(w.G1, layerIndex: 4); // this side's generation
		var received = Listen(w.G1);

		SendStampedSnapshot(w, w.G1, WorldGenerationReports.StampOf(w.G1, layerOverride: 3));
		w.Driver.Tick(50);

		// The snapshot IS on the wire — and refused whole before the layout
		// reaches the guest's world, so no previous-layer trap is materialized.
		Assert.Equal(1, w.ReceivedCount(w.G1, NetMsg.TrapLayoutSnapshot));
		Assert.Empty(received);
	}

	[Fact]
	public void SnapshotOfTheSameGeneration_IsApplied()
	{
		using var w = ItemSimWorld.Create();
		WorldGenerationReports.CommitRun(w.Host, layerIndex: 3);
		WorldGenerationReports.CommitRun(w.G1, layerIndex: 3);
		var received = Listen(w.G1);

		HostReportsOneTrap(w);
		w.Host.Services.GetRequiredService<IWorldControl>().SendTrapLayoutSnapshot(w.G1.SteamId);
		w.Driver.Tick(50);

		var entries = Assert.Single(received);
		Assert.Equal(Prefab, Assert.Single(entries).PrefabName);
	}

	[Fact]
	public void AStaleSnapshot_ReachesNeitherPeer_WhenBothHaveDescended()
	{
		using var w = ItemSimWorld.Create();
		WorldGenerationReports.CommitRun(w.G1, layerIndex: 4);
		WorldGenerationReports.CommitRun(w.G2, layerIndex: 4);
		var g1 = Listen(w.G1);
		var g2 = Listen(w.G2);

		SendStampedSnapshot(w, w.G1, WorldGenerationReports.StampOf(w.G1, layerOverride: 3));
		SendStampedSnapshot(w, w.G2, WorldGenerationReports.StampOf(w.G2, layerOverride: 3));
		w.Driver.Tick(50);

		// The refusal is a property of the generation comparison, not of one
		// peer's state: no third party materializes the stale layout either.
		Assert.Equal(1, w.ReceivedCount(w.G1, NetMsg.TrapLayoutSnapshot));
		Assert.Equal(1, w.ReceivedCount(w.G2, NetMsg.TrapLayoutSnapshot));
		Assert.Empty(g1);
		Assert.Empty(g2);
	}

	[Fact]
	public void ASnapshotWithoutAStamp_KeepsThePreStampBehaviour()
	{
		using var w = ItemSimWorld.Create();
		WorldGenerationReports.CommitRun(w.G1, layerIndex: 4); // a baseline here, none on the host: the report is UNKNOWN
		var received = Listen(w.G1);

		HostReportsOneTrap(w);
		w.Host.Services.GetRequiredService<IWorldControl>().SendTrapLayoutSnapshot(w.G1.SteamId);
		w.Driver.Tick(50);

		// UNKNOWN is never treated as STALE: a peer that cannot be attributed
		// keeps the behaviour the family had before the stamp existed.
		Assert.Single(received);
	}

	[Fact]
	public void EverySend_CarriesTheHostsOwnGenerationStamp()
	{
		using var w = ItemSimWorld.Create();
		WorldGenerationReports.CommitRun(w.Host, layerIndex: 2);
		var framed = new List<TrapLayoutSnapshotMsg>();
		w.G1.Transport.MessageReceived += (_, frame) =>
		{
			if ((NetMsg)frame[0] == NetMsg.TrapLayoutSnapshot)
			{
				framed.Add(NetPacket.DecodePayload<TrapLayoutSnapshotMsg>(frame));
			}
		};

		HostReportsOneTrap(w);
		w.Host.Services.GetRequiredService<IWorldControl>().SendTrapLayoutSnapshot(w.G1.SteamId);
		w.Driver.Tick(33);

		// The stamp is attached by the SEND PATH itself (the registry reads the
		// kernel run baseline at send time), never by the caller: what a receiver
		// compares is exactly this value.
		var expected = WorldGenerationReports.StampOf(w.Host);
		var msg = Assert.Single(framed);
		Assert.NotNull(msg.Generation);
		Assert.Equal(expected.RunEpoch, msg.Generation!.RunEpoch);
		Assert.Equal(expected.LayerIndex, msg.Generation.LayerIndex);
	}

	[Fact]
	public void TheEntryGroup_SendsTheRunBaselineBeforeEveryStampedTable()
	{
		using var w = ItemSimWorld.Create();
		WorldGenerationReports.CommitRun(w.Host, layerIndex: 3);
		HostReportsOneTrap(w);
		w.Host.Services.GetRequiredService<IWorldControl>().SendEntitySpawned(new EntitySpawnedMsg
		{
			Id = "landmine",
			Position = new NetVector2Msg(6f, 6f),
			CreatorSteamId = w.Host.SteamId,
			CreationSequence = 3,
		});
		var order = new List<NetMsg>();
		w.G1.Transport.MessageReceived += (_, frame) => order.Add((NetMsg)frame[0]);

		w.Host.Services.GetRequiredService<WorldEntryFanout>().Send(w.G1.SteamId);
		w.Driver.Tick(50);

		// The kernel checkpoint is what establishes the member's run baseline,
		// and every stamped absolute table is compared against it on arrival:
		// sending one BEFORE the checkpoint would make the receiver refuse the
		// host's own current-generation table (the entry-edge refusal the
		// independent review found). The trap layout and the runtime-entity
		// table must be present here; the block tables only go out when the
		// host's own tables have rows, which the adapter's block patch feeds
		// (not run by this host), so for them the rule is asserted whenever they
		// appear.
		var baseline = order.IndexOf(NetMsg.KernelEnvelope);
		Assert.True(baseline >= 0, $"the entry group must carry the kernel checkpoint, got {string.Join(",", order)}");
		AssertAfterBaseline(order, NetMsg.TrapLayoutSnapshot, baseline, required: true);
		AssertAfterBaseline(order, NetMsg.RuntimeEntitySnapshot, baseline, required: true);
		AssertAfterBaseline(order, NetMsg.WorldBlockState, baseline, required: false);
		AssertAfterBaseline(order, NetMsg.BlockDamageSnapshot, baseline, required: false);
	}

	private static void AssertAfterBaseline(List<NetMsg> order, NetMsg msg, int baseline, bool required)
	{
		var index = order.IndexOf(msg);
		Assert.True(index >= 0 || !required, $"{msg} must be part of the group, got {string.Join(",", order)}");
		Assert.True(index < 0 || index > baseline, $"{msg} must follow the run baseline, got {string.Join(",", order)}");
	}
}
