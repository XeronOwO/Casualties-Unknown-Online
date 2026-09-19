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
/// The runtime-entity creation family's world/layer generation identity
/// (review/generation-identity-remaining-families): a creation is materialized at
/// its reported position, and positions are layer-relative, so a report or an
/// absolute table describing the layer the host has just left used to create the
/// previous layer's entity in the new one. Every send (the live report, the
/// host's relay, the guest's fallback re-report and the host's absolute table)
/// carries the sender's kernel run baseline, the receiving seam refuses a STALE
/// one before anything is created or applied, and the host answers a refused
/// reporter through the existing rejection path so its pending re-report ends.
/// <para>
/// The host executor here is the same contract double the E3 recovery suite uses
/// (record + relay through the production host branch). What the send path
/// attaches is proven at frame level; the cross-layer arrivals carry an explicit
/// stamp, because this harness propagates the host's run baseline to the guests
/// and so cannot hold the two sides at different layers locally. The adapter's
/// materialization is game-typed and stays covered by the unified dual-client
/// acceptance pass; what these tests prove is the wire decision — what is
/// refused, what is answered, and what never reaches a third member.
/// </para>
/// </summary>
[Trait("Category", "Integration")]
public class RuntimeEntityGenerationIdentityTests
{
	private static EntitySpawnedMsg Creation(string id, float x, float y, ulong creator, uint sequence) => new()
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

	private static List<RuntimeEntityKey> ListenForRejections(TestNode guest)
	{
		var rejected = new List<RuntimeEntityKey>();
		guest.Services.GetRequiredService<IWorldControl>().RuntimeEntityRejectedReceived += (key, _) => rejected.Add(key);
		return rejected;
	}

	[Fact]
	public void ReportFromAPreviousLayer_IsRefusedWhole_Answered_AndNeverRelayed()
	{
		using var w = ItemSimWorld.Create();
		var reports = InstallHostExecutor(w);
		var rejected = ListenForRejections(w.G1);
		WorldGenerationReports.CommitRun(w.Host, layerIndex: 4); // the world this session is in now
		w.Driver.Tick(33); // the member follows the host's baseline before anything is outstanding

		// The report's live send is swallowed, so its pending entry survives —
		// nothing advances this side's generation inside the window.
		w.Driver.Network.SetFaults(w.G1.SteamId, w.Host.SteamId, new LinkFaults { Down = true });
		w.G1.Services.GetRequiredService<IWorldControl>().SendEntitySpawned(Creation("landmine", 6f, 6f, w.G1.SteamId, 4));
		w.Driver.Tick(33);
		Assert.Empty(reports);
		Assert.Equal(1, PendingCreations(w.G1));

		// The same report, as it was stamped before the descent, arrives late at
		// a host that has moved on: refused whole, and the reporter is answered
		// so its pending report ends.
		w.Driver.Network.ClearFaults(w.G1.SteamId, w.Host.SteamId);
		var overdue = Creation("landmine", 6f, 6f, w.G1.SteamId, 4);
		overdue.Generation = WorldGenerationReports.StampOf(w.Host, layerOverride: 3);
		w.G1.Services.GetRequiredService<PacketSender>().Send(w.Host.SteamId, NetMsg.EntitySpawned, overdue);
		w.Driver.Tick(33);

		Assert.Empty(reports); // neither materialized nor relayed
		Assert.Equal(0, w.Host.Services.GetRequiredService<RuntimeEntityRegistry>().Count);
		Assert.Equal(0, w.ReceivedCount(w.G2, NetMsg.EntitySpawned)); // the third member never sees it
		Assert.Single(rejected); // the reporter is answered through the rejection path
		Assert.Equal(1, w.ReceivedCount(w.G1, NetMsg.RuntimeEntityRejected));
		Assert.Equal(0, PendingCreations(w.G1)); // and its pending report ends there
	}

	[Fact]
	public void AStaleAnswer_EndsOnlyItsOwnCreation_NotAnotherOneAtTheSameCell()
	{
		using var w = ItemSimWorld.Create();
		var rejected = ListenForRejections(w.G1);
		WorldGenerationReports.CommitRun(w.Host, layerIndex: 4);
		w.Driver.Tick(33);

		// Two creations in the SAME cell, distinguished only by their creation
		// token; both live reports are swallowed, so both sit in the pending
		// table when the host answers one of them.
		w.Driver.Network.SetFaults(w.G1.SteamId, w.Host.SteamId, new LinkFaults { Down = true });
		w.G1.Services.GetRequiredService<IWorldControl>().SendEntitySpawned(Creation("landmine", 6f, 6f, w.G1.SteamId, 4));
		w.G1.Services.GetRequiredService<IWorldControl>().SendEntitySpawned(Creation("landmine", 6f, 6f, w.G1.SteamId, 5));
		w.Driver.Tick(33);
		Assert.Equal(2, PendingCreations(w.G1));

		// Only the FIRST creation's overdue report is answered — the answer is
		// keyed by creator + per-creator sequence, so the other creation at the
		// same cell keeps its pending report (and, adapter-side, its copy).
		w.Driver.Network.ClearFaults(w.G1.SteamId, w.Host.SteamId);
		var overdue = Creation("landmine", 6f, 6f, w.G1.SteamId, 4);
		overdue.Generation = WorldGenerationReports.StampOf(w.Host, layerOverride: 3);
		w.G1.Services.GetRequiredService<PacketSender>().Send(w.Host.SteamId, NetMsg.EntitySpawned, overdue);
		w.Driver.Tick(33);

		var key = Assert.Single(rejected);
		Assert.Equal(4u, key.CreationSequence);
		Assert.Equal(1, PendingCreations(w.G1)); // the other creation's report is untouched
		Assert.Equal(0, w.ReceivedCount(w.G2, NetMsg.EntitySpawned));
	}

	[Fact]
	public void ReportOfTheSameGeneration_IsAcceptedRelayedAndRecorded()
	{
		using var w = ItemSimWorld.Create();
		var reports = InstallHostExecutor(w);
		WorldGenerationReports.CommitRun(w.Host, layerIndex: 3);
		WorldGenerationReports.CommitRun(w.G1, layerIndex: 3);

		w.G1.Services.GetRequiredService<IWorldControl>().SendEntitySpawned(Creation("landmine", 6f, 6f, w.G1.SteamId, 4));
		w.Driver.Tick(33);

		var report = Assert.Single(reports);
		Assert.Equal(w.G1.SteamId, report.Sender);
		Assert.Equal(1, w.Host.Services.GetRequiredService<RuntimeEntityRegistry>().Count);
		Assert.True(w.ReceivedCount(w.G2, NetMsg.EntitySpawned) >= 1,
			$"the accepted relay must reach the other members, got {w.ReceivedCount(w.G2, NetMsg.EntitySpawned)}");
		Assert.Equal(0, PendingCreations(w.G1)); // the echo acknowledged it
	}

	[Fact]
	public void ReportWithoutAStamp_KeepsThePreStampBehaviour()
	{
		using var w = ItemSimWorld.Create();
		var reports = InstallHostExecutor(w);
		WorldGenerationReports.CommitRun(w.Host, layerIndex: 4); // a baseline here, none on the reporter: the report is UNKNOWN

		w.G1.Services.GetRequiredService<IWorldControl>().SendEntitySpawned(Creation("landmine", 6f, 6f, w.G1.SteamId, 4));
		w.Driver.Tick(33);

		// UNKNOWN is never treated as STALE: a reporter that cannot be attributed
		// keeps the behaviour the family had before the stamp existed.
		Assert.Single(reports);
		Assert.Equal(0, w.ReceivedCount(w.G1, NetMsg.RuntimeEntityRejected));
	}

	[Fact]
	public void TheLiveReport_CarriesTheReportersOwnGenerationStamp()
	{
		using var w = ItemSimWorld.Create();
		WorldGenerationReports.CommitRun(w.G1, layerIndex: 2);
		var framed = new List<EntitySpawnedMsg>();
		w.Host.Transport.MessageReceived += (_, frame) =>
		{
			if ((NetMsg)frame[0] == NetMsg.EntitySpawned)
			{
				framed.Add(NetPacket.DecodePayload<EntitySpawnedMsg>(frame));
			}
		};

		w.G1.Services.GetRequiredService<IWorldControl>().SendEntitySpawned(Creation("landmine", 6f, 6f, w.G1.SteamId, 4));
		w.Driver.Tick(33);

		// The stamp is attached by the SEND PATH itself (the channel reads the
		// kernel run baseline at send time), never by the caller.
		var expected = WorldGenerationReports.StampOf(w.G1);
		var msg = Assert.Single(framed);
		Assert.NotNull(msg.Generation);
		Assert.Equal(expected.RunEpoch, msg.Generation!.RunEpoch);
		Assert.Equal(expected.LayerIndex, msg.Generation.LayerIndex);
	}

	[Fact]
	public void TheFallbackReReport_CarriesTheCurrentGenerationStamp()
	{
		using var w = ItemSimWorld.Create();
		var reports = InstallHostExecutor(w);
		WorldGenerationReports.CommitRun(w.Host, layerIndex: 5);
		WorldGenerationReports.CommitRun(w.G1, layerIndex: 5);

		// The lazy-P2P window: the live creation report never lands.
		w.Driver.Network.SetFaults(w.G1.SteamId, w.Host.SteamId, new LinkFaults { Down = true });
		w.G1.Services.GetRequiredService<IWorldControl>().SendEntitySpawned(Creation("keypad", 12f, 34f, w.G1.SteamId, 7));
		w.Driver.Tick(33);
		Assert.Empty(reports);

		// The link heals; past the fallback window the re-report goes out with
		// THIS side's current generation, read live at send time. A pending
		// entry never crosses a boundary (the generation boundary clears the
		// table), so what this pins is the live read, not a boundary-crossing
		// variant — there is none by construction.
		w.Driver.Network.ClearFaults(w.G1.SteamId, w.Host.SteamId);
		w.Driver.Tick(61_000);

		var expected = WorldGenerationReports.StampOf(w.G1);
		var report = Assert.Single(reports);
		Assert.NotNull(report.Msg.Generation);
		Assert.Equal(expected.RunEpoch, report.Msg.Generation!.RunEpoch);
		Assert.Equal(expected.LayerIndex, report.Msg.Generation.LayerIndex);
	}

	[Fact]
	public void AStaleRelay_IsRefusedByTheGuest_AndTheGuestNeverAnswers()
	{
		using var w = ItemSimWorld.Create();
		WorldGenerationReports.CommitRun(w.G1, layerIndex: 4); // this side's generation
		var applied = new List<EntitySpawnedMsg>();
		var guestWorld = w.G1.Services.GetRequiredService<IWorldControl>();
		guestWorld.EntitySpawnedReceived += (_, msg) => applied.Add(msg);

		// A host relay of the layer this guest has already left.
		guestWorld.FireEntitySpawnedReceived(w.Host.SteamId, new EntitySpawnedMsg
		{
			Id = "landmine",
			Position = new NetVector2Msg(6f, 6f),
			CreatorSteamId = w.Host.SteamId,
			CreationSequence = 9,
			Generation = WorldGenerationReports.StampOf(w.G1, layerOverride: 3),
		});

		Assert.Empty(applied); // nothing is created here
		Assert.Equal(0, w.ReceivedCount(w.G1, NetMsg.RuntimeEntityRejected)); // only the host answers a report
	}

	[Fact]
	public void AStaleAbsoluteTable_IsRefusedWhole_BeforeAnyEntryIsApplied()
	{
		using var w = ItemSimWorld.Create();
		WorldGenerationReports.CommitRun(w.G1, layerIndex: 4);
		var applied = new List<EntitySpawnedMsg>();
		w.G1.Services.GetRequiredService<IWorldControl>().EntitySpawnedReceived += (_, msg) => applied.Add(msg);

		List<EntitySpawnedMsg> entries =
		[
			Creation("landmine", 6f, 6f, w.Host.SteamId, 9),
		];
		w.Host.Services.GetRequiredService<PacketSender>().Send(
			w.G1.SteamId,
			NetMsg.RuntimeEntitySnapshot,
			new RuntimeEntitySnapshotMsg
			{
				Entries = entries,
				Generation = WorldGenerationReports.StampOf(w.G1, layerOverride: 3),
			});
		w.Driver.Tick(33);

		// The table DID arrive — and was refused whole, so no entry of the
		// previous layer's world is materialized here.
		Assert.Equal(1, w.ReceivedCount(w.G1, NetMsg.RuntimeEntitySnapshot));
		Assert.Empty(applied);
	}
}
