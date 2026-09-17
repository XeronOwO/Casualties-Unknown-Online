using System.Collections.Generic;
using CasualtiesUnknownOnline.Runtime.Protocol;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using CasualtiesUnknownOnline.Runtime.Session.World;
using CasualtiesUnknownOnline.Tests.Fakes;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.World;

/// <summary>
/// A runtime creation the host can neither record nor own is REJECTED
/// (decision 161 + its corrected <c>AGENTS.md</c> accept-first precondition):
/// the host must not relay state it cannot represent, because an
/// accepted-but-unowned record has no owner whose death can retract it and
/// leaks into every later snapshot. The rejection is visible — the reporter is
/// answered with the creation key and the reason, drops its pending re-report
/// and destroys its local copy through the same death funnel the rest of the
/// entity mechanism uses.
/// <para>
/// The host executor here is the Game Adapter's failure branch as a contract
/// double (<c>ReportEntitySpawnUnmaterialized</c>), the same pattern as
/// <see cref="GuestEntityReportRecoveryTests"/>'s accepting double; the
/// adapter's materialization and its destruction half are game-typed (they
/// reach <c>Component.transform</c>/<c>GetComponent</c>) and are verified by the
/// unified dual-client acceptance pass. What the tests here prove is the
/// decision, the wire answer and the account: nothing is relayed or recorded,
/// the reporter's pending entry is cleared by the REJECTION (it receives no
/// echo at all), the 60 s fallback stops, and every refusal is idempotent.
/// </para>
/// </summary>
[Trait("Category", "Integration")]
public class RuntimeEntityRejectionTests
{
	private static EntitySpawnedMsg Creation(string id, float x, float y, ulong creator = 0, uint sequence = 0) => new()
	{
		Id = id,
		Position = new NetVector2Msg(x, y),
		Rotation = 0f,
		CreatorSteamId = creator,
		CreationSequence = sequence,
	};

	/// <summary>The host executor's FAILURE branch as a contract double: the host cannot materialize the reported prefab, so the channel rejects it.</summary>
	private static List<(ulong Sender, EntitySpawnedMsg Msg)> InstallRejectingHostExecutor(ItemSimWorld w)
	{
		var reports = new List<(ulong Sender, EntitySpawnedMsg Msg)>();
		var hostWorld = w.Host.Services.GetRequiredService<IWorldControl>();
		hostWorld.EntitySpawnedReceived += (sender, msg) =>
		{
			reports.Add((sender, msg));
			hostWorld.ReportEntitySpawnUnmaterialized(sender, msg);
		};
		return reports;
	}

	private static int PendingCreations(TestNode guest) =>
		guest.Services.GetRequiredService<RuntimeEntityChannel>().PendingEntityReportCount;

	[Fact]
	public void UnmaterializableGuestCreation_IsNeitherRelayedNorEchoed()
	{
		using var w = ItemSimWorld.Create();
		InstallRejectingHostExecutor(w);
		var guestWorld = w.G1.Services.GetRequiredService<IWorldControl>();

		guestWorld.SendEntitySpawned(Creation("modcrate", 6f, 6f, w.G1.SteamId, 4));
		w.Driver.Tick(33);

		// A creation the host cannot materialize is REJECTED: neither recorded
		// nor relayed. Relaying it would let every peer that happens to have the
		// prefab materialize an entity the host can never own, back up or
		// retract, and the reporter's own echo would hide the divergence.
		Assert.Equal(0, w.ReceivedCount(w.G2, NetMsg.EntitySpawned));
		Assert.Equal(0, w.ReceivedCount(w.G1, NetMsg.EntitySpawned));
		Assert.Equal(0, w.ReceivedCount(w.G2, NetMsg.RuntimeEntityRejected)); // the answer is targeted, never broadcast
		Assert.Equal(0, w.Host.Services.GetRequiredService<RuntimeEntityRegistry>().Count);
	}

	[Fact]
	public void RejectedCreation_AnswersTheReporterAndStopsTheFallback()
	{
		using var w = ItemSimWorld.Create();
		var reports = InstallRejectingHostExecutor(w);
		var guestWorld = w.G1.Services.GetRequiredService<IWorldControl>();
		var rejected = new List<RuntimeEntityKey>();
		guestWorld.RuntimeEntityRejectedReceived += (key, _) => rejected.Add(key);

		var key = new RuntimeEntityKey("modcrate", 6, 6, w.G1.SteamId, 4);
		guestWorld.SendEntitySpawned(Creation("modcrate", 6f, 6f, w.G1.SteamId, 4));
		w.Driver.Tick(33);

		// The rejection IS the report's answer: it reaches the reporter (who got
		// no echo — the creation was never relayed), drops the pending entry so
		// the 60 s fallback stops, and names the exact creation key the adapter
		// must destroy. Nothing was recorded, so no snapshot can resurrect it.
		Assert.Equal(1, w.ReceivedCount(w.G1, NetMsg.RuntimeEntityRejected));
		Assert.Equal(0, PendingCreations(w.G1));
		Assert.Equal(key, Assert.Single(rejected));

		w.Driver.Tick(61_000);
		Assert.Single(reports); // no fallback re-report: the rejection answered it
		Assert.Equal(0, w.ReceivedCount(w.G2, NetMsg.EntitySpawned));
		Assert.Equal(0, w.ReceivedCount(w.G2, NetMsg.RuntimeEntityRejected)); // third parties never learn of the creation
	}

	[Fact]
	public void LostRejection_TheFallbackReReports_AndTheHostRejectsAgainIdempotently()
	{
		using var w = ItemSimWorld.Create();
		var reports = InstallRejectingHostExecutor(w);
		var guestWorld = w.G1.Services.GetRequiredService<IWorldControl>();
		var rejected = new List<RuntimeEntityKey>();
		guestWorld.RuntimeEntityRejectedReceived += (key, _) => rejected.Add(key);

		// The rejection is swallowed on the way back: the reporter keeps its
		// pending entry and re-reports through the fallback, and the host rejects
		// the repeat identically — no record appears, no relay goes out, and the
		// creation converges the moment one rejection lands.
		w.Driver.Network.SetFaults(w.Host.SteamId, w.G1.SteamId, new LinkFaults { Down = true });
		guestWorld.SendEntitySpawned(Creation("modcrate", 6f, 6f, w.G1.SteamId, 4));
		w.Driver.Tick(33);
		Assert.Single(reports);
		Assert.Equal(1, PendingCreations(w.G1));
		Assert.Empty(rejected);

		w.Driver.Network.ClearFaults(w.Host.SteamId, w.G1.SteamId);
		w.Driver.Tick(61_000);

		Assert.Equal(2, reports.Count);
		Assert.Equal(1, w.ReceivedCount(w.G1, NetMsg.RuntimeEntityRejected));
		Assert.Equal(0, PendingCreations(w.G1));
		Assert.Single(rejected);
		Assert.Equal(0, w.ReceivedCount(w.G2, NetMsg.EntitySpawned));
		Assert.Equal(0, w.Host.Services.GetRequiredService<RuntimeEntityRegistry>().Count);
	}

	[Fact]
	public void RejectionForAnotherCreatorsKey_ChangesNothing()
	{
		using var w = ItemSimWorld.Create();
		var g2World = w.G2.Services.GetRequiredService<IWorldControl>();
		var rejected = new List<RuntimeEntityKey>();
		g2World.RuntimeEntityRejectedReceived += (key, _) => rejected.Add(key);

		// A rejection is answered to its reporter only, so this is the shape a
		// misdelivered or replayed one would have at another member: the key's
		// creator half is not this member's creation, so nothing is dropped and
		// nothing is marked for removal.
		g2World.FireRuntimeEntityRejectedReceived(w.Host.SteamId, new RuntimeEntityRejectedMsg
		{
			Key = new RuntimeEntityKeyMsg
			{
				Id = "modcrate",
				CellX = 6,
				CellY = 6,
				CreatorSteamId = w.G1.SteamId,
				CreationSequence = 4,
			},
			Reason = RuntimeEntityRejectReason.PrefabUnavailable,
		});

		Assert.Empty(rejected);
		Assert.Equal(0, PendingCreations(w.G2));
	}

	[Fact]
	public void RejectionForAKeyThisMemberReported_IsActedOnEvenWhenTheTokenNamesAnotherCreator()
	{
		using var w = ItemSimWorld.Create();
		var reports = InstallRejectingHostExecutor(w);
		var guestWorld = w.G1.Services.GetRequiredService<IWorldControl>();
		var rejected = new List<RuntimeEntityKey>();
		guestWorld.RuntimeEntityRejectedReceived += (key, _) => rejected.Add(key);

		// A hand-built report (a mod calling the public send surface) can name
		// another member in its creation token. The pending table is what proves
		// THIS member reported it, so the host's rejection must still clear the
		// entry and remove the copy — the creator half alone would leave the
		// reporter re-reporting for the rest of the session.
		var foreign = Creation("modcrate", 6f, 6f, w.G2.SteamId, 9);
		guestWorld.SendEntitySpawned(foreign);
		w.Driver.Tick(33);

		Assert.Single(reports);
		Assert.Equal(1, w.ReceivedCount(w.G1, NetMsg.RuntimeEntityRejected));
		Assert.Equal(RuntimeEntityKey.From(foreign), Assert.Single(rejected));
		Assert.Equal(0, PendingCreations(w.G1));
		Assert.Equal(0, w.ReceivedCount(w.G2, NetMsg.EntitySpawned));
		Assert.Equal(0, w.ReceivedCount(w.G2, NetMsg.RuntimeEntityRejected));
	}

	[Fact]
	public void RepeatedRejection_ReRaisesTheRemovalRequest_AndTheChannelStaysConsistent()
	{
		using var w = ItemSimWorld.Create();
		var guestWorld = w.G1.Services.GetRequiredService<IWorldControl>();
		var rejected = new List<RuntimeEntityKey>();
		guestWorld.RuntimeEntityRejectedReceived += (key, _) => rejected.Add(key);
		var msg = new RuntimeEntityRejectedMsg
		{
			Key = new RuntimeEntityKeyMsg
			{
				Id = "modcrate",
				CellX = 6,
				CellY = 6,
				CreatorSteamId = w.G1.SteamId,
				CreationSequence = 4,
			},
			Reason = RuntimeEntityRejectReason.PrefabUnavailable,
		};

		// A duplicate rejection (a resend, or one arriving after the local copy
		// already died) must not resurrect anything or throw. The channel keeps
		// re-raising the removal request — it cannot know whether the copy is
		// still there — so the idempotence lives in the adapter, whose
		// find-by-creation-key lookup is a logged no-op for a copy that is gone
		// (game-typed: code-reviewed, not executed here).
		guestWorld.FireRuntimeEntityRejectedReceived(w.Host.SteamId, msg);
		guestWorld.FireRuntimeEntityRejectedReceived(w.Host.SteamId, msg);

		Assert.Equal(2, rejected.Count); // the channel's half: consistent, no throw, nothing pending
		Assert.Equal(0, PendingCreations(w.G1));
	}
}
