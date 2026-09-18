using System.Collections.Generic;
using System.Linq;
using CasualtiesUnknownOnline.Runtime.Protocol;
using CasualtiesUnknownOnline.Runtime.Session;
using CasualtiesUnknownOnline.Runtime.Session.Items;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using CasualtiesUnknownOnline.Runtime.Session.World;
using CasualtiesUnknownOnline.Tests.Fakes;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.World;

/// <summary>
/// Guest block-break DROP recovery (sync-coverage audit W1's drop half). A
/// guest's break travels as two messages — the air write (W1, landed) and, one
/// frame later, one BlockDamaged carrying the break plus every drop. The guest
/// registers its drops nowhere, so the host learns them exclusively from that
/// second message: a swallowed one left items the authoritative table never knew
/// about and the item keyframe could not heal (it has no fact to reconcile from),
/// and the drops-carrying report arriving while the host's block still stood was
/// applied as damage only. The guest now records the break's drops in
/// <see cref="PendingBreakDropTable"/> BEFORE the live send, the 60 s fallback
/// re-reports the outstanding set, and the host's relay — which now includes the
/// reporter — is the acknowledgement that clears it. The duplicate guard is the
/// drop's ITEM ID, so a re-report can never double-register.
/// <para>
/// The host executor here is a contract double for the Game Adapter's thin shell
/// (the same pattern as BlockBreakSimulationTests): it applies the real
/// <see cref="BlockBreakArbitration"/> verdict, registers the accepted drops into
/// the real authoritative item table and relays the break. What this suite does
/// NOT execute is the adapter's own half — the recording call inside
/// <c>BlockBreakSync.FlushPendingBlockBreak</c> (a Unity type), the world→cell
/// conversion, the drop materialization into the scene, and the local-copy
/// protection against the keyframe. Those rest on code review plus the unified
/// dual-client acceptance pass, and the ticket says so.
/// </para>
/// </summary>
[Trait("Category", "Integration")]
public class GuestBreakDropRecoveryTests
{
	private const int CellX = 5;
	private const int CellY = 7;
	private const float Damage = 100f;

	/// <summary>One report as it travels: the break position plus both drop families.</summary>
	private sealed record Report(NetVector2 Pos, IReadOnlyList<BlockDropEntryMsg>? Drops, IReadOnlyList<TrapDropEntryMsg>? BuildingDrops);

	/// <summary>The host executor contract double: the production BlockBreakSync shape (verdict → register/materialize/relay, or refuse every drop), with the drops it registered recorded for the assertions.</summary>
	private sealed class HostExecutor(ItemSimWorld w)
	{
		private readonly ItemSimWorld _w = w;

		internal BlockBreakArbitration Arbitration { get; } = new();

		/// <summary>Every block drop id the host registered (its authoritative table holds them).</summary>
		internal List<ulong> Registered { get; } = [];

		/// <summary>Every relay the host sent, in order.</summary>
		internal List<Report> Relays { get; } = [];

		internal void Install()
		{
			var world = _w.Host.Services.GetRequiredService<IWorldControl>();
			var items = _w.Host.Services.GetRequiredService<IItemControl>();
			world.BlockDamagedReceived += (sender, pos, damage, metalBonus, drops, buildingDrops) =>
			{
				var cellX = (int)pos.X;
				var cellY = (int)pos.Y;
				var verdict = Arbitration.TryAccept(sender, cellX, cellY);
				if (verdict == Verdict.Refused)
				{
					foreach (var drop in drops ?? [])
					{
						items.SendItemReject(sender, drop.ItemId, ItemRejectMsg.Reason.BlockAlreadyBroken);
					}

					foreach (var drop in buildingDrops ?? [])
					{
						items.SendItemReject(sender, drop.ItemId, ItemRejectMsg.Reason.BlockAlreadyBroken);
					}

					return;
				}

				items.FireBlockDropsReceived(sender, drops ?? []);
				items.FireBuildingDropsReceived(sender, buildingDrops ?? []);
				// The host's copy of the cell is air from here on (a standing-block
				// report applies its own break, a fresh one consumed the air write).
				StandingBlocks.Remove((cellX, cellY));
				Arbitration.RecordAccepted(sender, cellX, cellY, now: _w.Driver.NowMs / 1000f);

				foreach (var drop in drops ?? [])
				{
					Registered.Add(drop.ItemId);
				}

				Relays.Add(new Report(pos, drops, buildingDrops));
				// Production relays to EVERYONE, the reporter included: that echo is
				// the acknowledgement that clears the reporter's pending drop report
				// (the block-state echo says nothing about whether the drops arrived).
				world.BroadcastBlockDamaged(0, pos, damage, metalBonus, drops, buildingDrops);
			};
		}

		/// <summary>The cells the host's world still holds a block for (the standing-block case: the guest's air write never landed).</summary>
		internal HashSet<(int X, int Y)> StandingBlocks { get; } = [];

		/// <summary>The host applied the sender's air write (its BlockPlaced report landed).</summary>
		internal void AirWriteLanded(ulong sender, int cellX, int cellY) =>
			Arbitration.RecordAppliedAirWrite(sender, cellX, cellY, now: _w.Driver.NowMs / 1000f);
	}

	/// <summary>The guest executor contract double: the production BlockBreakSync guest branch — the relay naming this break's drops answers the pending report (the cell comes from the same conversion the adapter makes).</summary>
	private static void InstallGuestExecutor(ItemSimWorld w, TestNode guest)
	{
		var world = guest.Services.GetRequiredService<IWorldControl>();
		world.BlockDamagedReceived += (sender, pos, damage, metalBonus, drops, buildingDrops) =>
		{
			if (sender == w.Host.SteamId && (drops is { Count: > 0 } || buildingDrops is { Count: > 0 }))
			{
				world.AnswerBreakDrops((int)pos.X, (int)pos.Y, drops, buildingDrops);
			}
		};
	}

	/// <summary>How many drop sets this guest still waits to have answered (the fallback pump's work check).</summary>
	private static int PendingDropReports(TestNode guest) =>
		guest.Services.GetRequiredService<WorldService>().PendingBreakDropReportCount;

	/// <summary>The guest's live break report — the wire shape BlockBreakSync.FlushPendingBlockBreak sends (drops only; a break with no drops reports nothing).</summary>
	private static void SendBreakReport(ItemSimWorld w, TestNode guest, int cellX, int cellY, IReadOnlyList<BlockDropEntryMsg>? drops, IReadOnlyList<TrapDropEntryMsg>? buildingDrops = null)
	{
		var sender = guest.Services.GetRequiredService<PacketSender>();
		sender.Send(w.Host.SteamId, NetMsg.BlockDamaged, new BlockDamagedMsg
		{
			Position = new NetVector2Msg(cellX, cellY),
			Damage = Damage,
			Drops = drops is { Count: > 0 } ? [.. drops] : null,
			BuildingDrops = buildingDrops is { Count: > 0 } ? [.. buildingDrops] : null,
		});
	}

	/// <summary>The guest side of a break: record the drops (the adapter's pre-send call) and send the live report.</summary>
	private static void BreakBlock(ItemSimWorld w, TestNode guest, int cellX, int cellY, IReadOnlyList<BlockDropEntryMsg>? drops, IReadOnlyList<TrapDropEntryMsg>? buildingDrops = null)
	{
		guest.Services.GetRequiredService<IWorldControl>()
			.ReportBreakDrops(cellX, cellY, cellX, cellY, drops, buildingDrops);
		SendBreakReport(w, guest, cellX, cellY, drops, buildingDrops);
	}

	private static BlockDropEntryMsg Drop(ulong itemId) => new()
	{
		ItemId = itemId,
		Item = new CharacterItemMsg { ItemId = "metalscrap", Condition = 1f },
		Position = new NetVector2Msg(CellX, CellY),
	};

	[Fact]
	public void SwallowedBreakReport_TheFallbackReReportIsWhatRegistersTheDrop()
	{
		using var w = ItemSimWorld.Create();
		var host = new HostExecutor(w);
		host.Install();
		host.AirWriteLanded(w.G1.SteamId, CellX, CellY); // the air write DID land — the host's cell is air
		InstallGuestExecutor(w, w.G1);
		var items = w.Items;

		// Only the drops report is swallowed. The host knows the break is this
		// guest's (its air write applied), so nothing inline can register the drops
		// here: the FALLBACK re-report is the only remaining carrier, which is what
		// this test must pin (an inline accept would make the assertion pass for
		// the wrong reason).
		w.Driver.Network.SetFaults(w.G1.SteamId, w.Host.SteamId, new LinkFaults { Down = true });
		BreakBlock(w, w.G1, CellX, CellY, [Drop(77)]);
		w.Driver.Tick(33);

		Assert.Equal(1, PendingDropReports(w.G1));
		Assert.Empty(host.Registered);
		Assert.False(items.IsWorldItemRegistered(77), "the host must not know the drop yet — only the report carries it");

		// Inside the window nothing is re-sent; past it the fallback re-reports the
		// break WITH its drops, and the host registers them in its table.
		w.Driver.Network.ClearFaults(w.G1.SteamId, w.Host.SteamId);
		w.Driver.Tick(59_000);
		Assert.Empty(host.Registered);

		w.Driver.Tick(2_000);
		Assert.Equal([77ul], host.Registered);
		Assert.True(items.IsWorldItemRegistered(77), "the host's authoritative table must hold the recovered drop");
		Assert.Equal(0, PendingDropReports(w.G1)); // the relay echoed back and answered it
	}

	[Fact]
	public void DropsFreeBreak_TheHostNeverAnswersIt_WhichIsWhyTheAdapterDoesNotRecordIt()
	{
		using var w = ItemSimWorld.Create();
		var host = new HostExecutor(w);
		host.Install();
		host.AirWriteLanded(w.G1.SteamId, CellX, CellY);
		InstallGuestExecutor(w, w.G1);

		// The premise the adapter's recording guard rests on: a break report with no
		// drop payload takes the host's DAMAGE-ONLY path, which relays no payload —
		// so no answer can ever name an (empty) item set, and a recorded entry would
		// be unanswerable and park a cap slot for the session. A zero-drop break is
		// reachable in the game (the drop roll has `Random.value < 0.5f` branches).
		//
		// NOTE: the guard itself lives in `BlockBreakSync.FlushPendingBlockBreak`, a
		// Unity method this suite does not execute (see the class doc); what this
		// test pins is the host-side premise, not the guard.
		w.Driver.Network.SetFaults(w.G1.SteamId, w.Host.SteamId, new LinkFaults { Down = true });
		SendBreakReport(w, w.G1, CellX, CellY, null, null);
		w.Driver.Tick(33);

		Assert.Empty(host.Registered);
		Assert.Empty(host.Relays);

		// Even once the link heals, a payload-free report is never relayed: there is
		// nothing for a host answer to name.
		w.Driver.Network.ClearFaults(w.G1.SteamId, w.Host.SteamId);
		w.Driver.Tick(61_000);
		Assert.Empty(host.Registered);
		Assert.Empty(host.Relays);
	}

	[Fact]
	public void SessionEnd_ClearsThePendingDropReports()
	{
		using var w = ItemSimWorld.Create();
		var host = new HostExecutor(w);
		host.Install();
		host.AirWriteLanded(w.G1.SteamId, CellX, CellY);
		InstallGuestExecutor(w, w.G1);
		var guestWorld = w.G1.Services.GetRequiredService<WorldService>();

		w.Driver.Network.SetFaults(w.G1.SteamId, w.Host.SteamId, new LinkFaults { Down = true });
		BreakBlock(w, w.G1, CellX, CellY, [Drop(87)]);
		w.Driver.Tick(33);
		Assert.Equal(1, guestWorld.PendingBreakDropReportCount);

		// The session ended: the next one must not inherit the previous world's
		// unanswered break (the other two pending channels have the same case).
		w.G1.Session.EndSession();

		Assert.Equal(0, guestWorld.PendingBreakDropReportCount);
		Assert.False(w.G1.Services.GetRequiredService<IWorldControl>().IsBreakDropPending(87));
	}

	[Fact]
	public void LostAirWrite_TheBreakReportIsRefused_AndTheRefusalReachesTheBreaker()
	{
		using var w = ItemSimWorld.Create();
		var host = new HostExecutor(w);
		host.Install();
		host.StandingBlocks.Add((CellX, CellY)); // the report's air write never landed; the host still holds the block
		InstallGuestExecutor(w, w.G1);
		var items = w.Items;

		BreakBlock(w, w.G1, CellX, CellY, [Drop(78)]);
		w.Driver.Tick(33);

		// A report naming a cell the host still holds is NOT attributed: the block's
		// own state looks like evidence of a first writer, but the record would be
		// layer-relative while the report is not — after a descent a stale report
		// would name a freshly generated block and its real damage would break it.
		// So the drops are refused (destroyed on the breaker, whose break the
		// air-write report still converges on the host through W1) instead of being
		// registered on unattributable evidence.
		Assert.Empty(host.Registered);
		Assert.False(items.IsWorldItemRegistered(78));
		Assert.Equal((CellX, CellY), Assert.Single(host.StandingBlocks));
		var reject = Assert.Single(w.Rejects(w.G1));
		Assert.True(reject.ItemId == 78, $"the refused drop must be named to the breaker, got {reject.ItemId}");

		// The refusal's local half is the adapter's (it destroys the refused drop and
		// forgets it from the pending set); the double models that step here so the
		// test proves the re-report actually stops, instead of parking a cap slot.
		w.G1.Services.GetRequiredService<IWorldControl>().ForgetBreakDrop(78);
		Assert.Equal(0, PendingDropReports(w.G1));
	}

	[Fact]
	public void LostDropsReport_IsReReported_AndAnsweredByTheRelayEcho()
	{
		using var w = ItemSimWorld.Create();
		var host = new HostExecutor(w);
		host.Install();
		InstallGuestExecutor(w, w.G1);
		var items = w.Items;

		// The host already accepted this guest's break for the cell (its air write
		// landed and was consumed by the earlier report), so the cell is air and the
		// break is attributed — only the drops report is lost now.
		host.Arbitration.RecordAccepted(w.G1.SteamId, CellX, CellY, now: 0f);
		w.Driver.Network.SetFaults(w.G1.SteamId, w.Host.SteamId, new LinkFaults { Down = true });
		BreakBlock(w, w.G1, CellX, CellY, [Drop(79)]);
		w.Driver.Tick(33);
		Assert.Equal(1, PendingDropReports(w.G1));

		w.Driver.Network.ClearFaults(w.G1.SteamId, w.Host.SteamId);
		w.Driver.Tick(61_000);

		Assert.Equal([79ul], host.Registered);
		Assert.True(items.IsWorldItemRegistered(79));
		Assert.Equal(0, PendingDropReports(w.G1));

		// The answer cleared the entry: the next window re-sends nothing.
		w.Driver.Tick(61_000);
		Assert.Equal([79ul], host.Registered);
	}

	[Fact]
	public void DuplicateReReport_RegistersAndMaterializesExactlyOncePerDrop()
	{
		using var w = ItemSimWorld.Create();
		var host = new HostExecutor(w);
		host.Install();
		InstallGuestExecutor(w, w.G1);
		host.AirWriteLanded(w.G1.SteamId, CellX, CellY);

		BreakBlock(w, w.G1, CellX, CellY, [Drop(80)]);
		w.Driver.Tick(33);
		Assert.Equal([80ul], host.Registered);
		Assert.Equal(0, PendingDropReports(w.G1));

		// A duplicate report of the same break (a re-send whose answer was lost)
		// takes the idempotent repeat path: the host re-registers the same item id
		// (a no-op in its table) and re-relays, and no peer sees a second
		// materialization — the guard is the item id, never the cell alone.
		SendBreakReport(w, w.G1, CellX, CellY, [Drop(80)]);
		w.Driver.Tick(33);

		Assert.Equal([80ul, 80ul], host.Registered);
		Assert.Single(w.Items.GetWorldItemsForDiagnostics().Where(i => i.ItemId == 80));
		Assert.True(w.ReceivedCount(w.G2, NetMsg.BlockDamaged) >= 2, "the third member sees the relay (its materialization is idempotent per item id)");
	}

	[Fact]
	public void AnotherSendersBreakOfTheSameCell_RefusesTheLosersDrops()
	{
		using var w = ItemSimWorld.Create();
		var host = new HostExecutor(w);
		host.Install();
		InstallGuestExecutor(w, w.G1);
		InstallGuestExecutor(w, w.G2);
		host.AirWriteLanded(w.G1.SteamId, CellX, CellY);

		// G1's break wins the cell.
		BreakBlock(w, w.G1, CellX, CellY, [Drop(81)]);
		w.Driver.Tick(33);
		Assert.Equal([81ul], host.Registered);

		// G2's break of the same cell has no air-write record of its own: first
		// writer wins, the loser's drops are refused and it destroys its local copy.
		BreakBlock(w, w.G2, CellX, CellY, [Drop(82)]);
		w.Driver.Tick(33);

		Assert.Equal([81ul], host.Registered);
		Assert.False(w.HostTable(82));
		var reject = Assert.Single(w.Rejects(w.G2));
		Assert.True(reject.ItemId == 82, $"the loser's drop must be rejected, got {reject.ItemId}");

		// The refusal's local half — the adapter destroys the refused drop and
		// forgets it from the pending set (ItemApplication.OnItemRejected) — is
		// adapter-side and not executed here; the forget surface itself is covered
		// by PendingBreakDropTableTests.ForgetItem and the pending-count case below.
		w.G2.Services.GetRequiredService<IWorldControl>().ForgetBreakDrop(82);
		Assert.Equal(0, PendingDropReports(w.G2));
	}

	[Fact]
	public void ThirdParty_SeesTheSameDropIdentities()
	{
		using var w = ItemSimWorld.Create();
		var host = new HostExecutor(w);
		host.Install();
		InstallGuestExecutor(w, w.G1);
		host.AirWriteLanded(w.G1.SteamId, CellX, CellY);

		BreakBlock(w, w.G1, CellX, CellY, [Drop(83)]);
		w.Driver.Tick(33);

		// The relay carries the SAME item id to every member (the materialization
		// itself is adapter-side and is covered by the dual-client pass).
		var relay = Assert.Single(host.Relays);
		Assert.Equal(83ul, Assert.Single(relay.Drops!).ItemId);
		Assert.True(w.ReceivedCount(w.G2, NetMsg.BlockDamaged) >= 1, "the third member receives the break's drops");
	}

	[Fact]
	public void WorldReset_DropsThePreviousWorldsPendingReports()
	{
		using var w = ItemSimWorld.Create();
		var host = new HostExecutor(w);
		host.Install();
		host.AirWriteLanded(w.G1.SteamId, CellX, CellY);
		InstallGuestExecutor(w, w.G1);

		w.Driver.Network.SetFaults(w.G1.SteamId, w.Host.SteamId, new LinkFaults { Down = true });
		BreakBlock(w, w.G1, CellX, CellY, [Drop(84)]);
		w.Driver.Tick(33);
		Assert.Equal(1, PendingDropReports(w.G1));
		w.Driver.Network.ClearFaults(w.G1.SteamId, w.Host.SteamId);

		// A new world/layer baseline: a previous world's break must never be
		// arbitrated (or registered) into the new one.
		w.G1.Services.GetRequiredService<IWorldControl>().ResetPendingBreakDropReports();
		w.Driver.Tick(61_000);

		Assert.Empty(host.Registered);
		Assert.Equal(0, PendingDropReports(w.G1));
	}

	[Fact]
	public void HostRole_NeverRecordsAPendingDropReport()
	{
		using var w = ItemSimWorld.Create();
		var hostWorld = w.Host.Services.GetRequiredService<IWorldControl>();

		hostWorld.ReportBreakDrops(CellX, CellY, CellX, CellY, [Drop(85)], null);
		w.Driver.Tick(61_000);

		Assert.Equal(0, w.Host.Services.GetRequiredService<WorldService>().PendingBreakDropReportCount);
		Assert.False(hostWorld.IsBreakDropPending(85));
	}

	[Fact]
	public void PendingDrop_IsQueryableUntilTheHostAnswers()
	{
		using var w = ItemSimWorld.Create();
		var host = new HostExecutor(w);
		host.Install();
		host.AirWriteLanded(w.G1.SteamId, CellX, CellY); // the break IS attributable, so the report is outstanding
		InstallGuestExecutor(w, w.G1);
		var guestWorld = w.G1.Services.GetRequiredService<IWorldControl>();

		w.Driver.Network.SetFaults(w.G1.SteamId, w.Host.SteamId, new LinkFaults { Down = true });
		BreakBlock(w, w.G1, CellX, CellY, [Drop(86)]);
		w.Driver.Tick(33);

		// The item keyframe's reconcile asks this before killing a locally-created
		// drop: the 5-30 s keyframe is far inside the 60 s fallback, so an
		// unacknowledged drop must survive it.
		Assert.True(guestWorld.IsBreakDropPending(86));
		Assert.False(guestWorld.IsBreakDropPending(999));

		w.Driver.Network.ClearFaults(w.G1.SteamId, w.Host.SteamId);
		w.Driver.Tick(61_000);

		Assert.False(guestWorld.IsBreakDropPending(86));
	}

	[Fact]
	public void RepeatAfterTheCellWasRebuilt_IsStillAcknowledged_NotRefused()
	{
		using var w = ItemSimWorld.Create();
		var host = new HostExecutor(w);
		host.Install();
		host.AirWriteLanded(w.G1.SteamId, CellX, CellY);
		InstallGuestExecutor(w, w.G1);

		// The break is accepted and its drops registered.
		BreakBlock(w, w.G1, CellX, CellY, [Drop(88)]);
		w.Driver.Tick(33);
		Assert.Equal([88ul], host.Registered);
		Assert.Equal(0, PendingDropReports(w.G1));

		// Somebody rebuilds the cell (rebuilding a hole in a base is the normal case)
		// and the breaker's acknowledgement relay is lost, so its fallback re-reports.
		// The cell now holds a block again: a REPEAT must still be acknowledged, or
		// the refusal would destroy a drop the host's own table already holds.
		host.StandingBlocks.Add((CellX, CellY));
		SendBreakReport(w, w.G1, CellX, CellY, [Drop(88)]);
		w.Driver.Tick(33);

		Assert.True(w.Items.IsWorldItemRegistered(88), "an already-registered drop must survive a re-occupied cell");
		Assert.Empty(w.Rejects(w.G1));
	}
}
