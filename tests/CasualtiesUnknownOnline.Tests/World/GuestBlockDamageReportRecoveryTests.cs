using System.Collections.Generic;
using System.Linq;
using CasualtiesUnknownOnline.Runtime.Protocol;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using CasualtiesUnknownOnline.Runtime.Session.World;
using CasualtiesUnknownOnline.Tests.Fakes;
using CasualtiesUnknownOnline.Tests.Persistence;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.World;

/// <summary>
/// Guest partial block-damage report recovery (sync-coverage audit W2). The live
/// partial-damage report is a DELTA (the receiver accumulates it) and the host's
/// authoritative table is the GAME's own <c>blockDamages</c> list, so a swallowed
/// report leaves the host short by exactly that hit — and the host's absolute
/// snapshot, which reads that list at send time, then omits the cell and can
/// never heal either side. The guest keeps the cells' ABSOLUTE damage in
/// <see cref="PendingBlockDamageTable"/>, re-reports them on the 60 s fallback
/// cycle, and drops each entry when the host answers for that cell (the snapshot
/// / the per-report answer) or the cell goes air.
/// <para>
/// The native damage table is faked at the <see cref="INativeWorldFacts"/> seam,
/// so the Runtime-owned half of the loop (record → re-report → merge through the
/// port → answer → clear) runs for real, and the fake's own call log is the
/// arrival evidence. What this suite does NOT execute is the adapter's half —
/// the record-before-send order and the absolute read inside
/// <c>BlockBreakSync.OnBlockDamaged</c> (a Unity type), the engine-side merge
/// reads and the crack-sprite work of <c>GameBlockDamageTable.Merge</c>, and the
/// zero-row clearing through <c>BlockDamageCleaner</c>. Those rest on code review
/// plus the unified dual-client acceptance pass, and the ticket says so.
/// </para>
/// </summary>
[Trait("Category", "Integration")]
public class GuestBlockDamageReportRecoveryTests
{
	private const float SwallowedDamage = 20f;

	/// <summary>The lazy-P2P window: the guest applied the damage and recorded the cell, but the live delta report never lands.</summary>
	private static void SwallowGuestReport(ItemSimWorld w, IWorldControl guestWorld, int x, int y, float damage)
	{
		w.Driver.Network.SetFaults(w.G1.SteamId, w.Host.SteamId, new LinkFaults { Down = true });
		guestWorld.ReportBlockDamage(x, y, damage);
		guestWorld.SendBlockDamaged(new NetVector2(x + 0.5f, y + 0.5f), damage, false, null, null);
		w.Driver.Tick(33);

		// The link heals before the fallback window elapses.
		w.Driver.Network.ClearFaults(w.G1.SteamId, w.Host.SteamId);
	}

	/// <summary>How many absolute reports the host actually merged — the fake port records every call it takes.</summary>
	private static int MergeCalls(FakeNativeWorldFacts native) =>
		native.Calls.Count(call => call == "merge-block-damages");

	/// <summary>The guest's outstanding-report count (the fallback pump's work check).</summary>
	private static int PendingDamageReports(ItemSimWorld w) =>
		w.G1.Services.GetRequiredService<WorldService>().PendingBlockDamageReportCount;

	[Fact]
	public void SwallowedGuestPartialDamageReport_ConvergesOnTheFallbackCycle()
	{
		var native = new FakeNativeWorldFacts();
		using var w = ItemSimWorld.Create(services => services.AddSingleton<INativeWorldFacts>(native));
		var guestWorld = w.G1.Services.GetRequiredService<IWorldControl>();

		SwallowGuestReport(w, guestWorld, 5, 7, SwallowedDamage);

		Assert.Empty(native.Damages);

		// Inside the 60 s window nothing is re-sent (the live report just went
		// out); past it the fallback hands the host the ABSOLUTE value.
		w.Driver.Tick(59_000);
		Assert.Empty(native.Damages);
		w.Driver.Tick(2_000);

		Assert.Contains(native.Damages, row => row.X == 5 && row.Y == 7 && row.Damage == SwallowedDamage);
		Assert.Equal(1, MergeCalls(native));
	}

	[Fact]
	public void HostsAnswer_ClearsThePendingReport()
	{
		var native = new FakeNativeWorldFacts();
		using var w = ItemSimWorld.Create(services => services.AddSingleton<INativeWorldFacts>(native));
		var guestWorld = w.G1.Services.GetRequiredService<IWorldControl>();

		SwallowGuestReport(w, guestWorld, 5, 7, SwallowedDamage);
		w.Driver.Tick(61_000);

		Assert.Equal(SwallowedDamage, Assert.Single(native.Damages).Damage);
		Assert.Equal(1, MergeCalls(native));
		Assert.Equal(0, PendingDamageReports(w));

		// The answer cleared the pending entry: the next window re-sends nothing.
		w.Driver.Tick(61_000);
		Assert.Equal(1, MergeCalls(native));
	}

	[Fact]
	public void ThirdParty_ReceivesTheHostsAuthoritativeValue()
	{
		var native = new FakeNativeWorldFacts();
		using var w = ItemSimWorld.Create(services => services.AddSingleton<INativeWorldFacts>(native));
		var guestWorld = w.G1.Services.GetRequiredService<IWorldControl>();
		var seen = new List<IReadOnlyList<BlockDamageEntryMsg>>();
		w.G2.Services.GetRequiredService<IWorldControl>().BlockDamageSnapshotReceived += entries => seen.Add(entries);

		SwallowGuestReport(w, guestWorld, 5, 7, SwallowedDamage);
		w.Driver.Tick(61_000);

		var row = Assert.Single(Assert.Single(seen));
		Assert.True(row.X == 5 && row.Y == 7 && row.Damage == SwallowedDamage,
			$"the third member must see the authoritative row, got ({row.X},{row.Y})/{row.Damage}");
	}

	[Fact]
	public void RefusedReport_IsAnsweredWithZeroAndClearsThePendingReport()
	{
		// The shape a report the host's own cap/range rules refused comes back as:
		// every cell answered, the refused one with an explicit zero.
		var native = new FakeNativeWorldFacts { MergesAreRefused = true };
		using var w = ItemSimWorld.Create(services => services.AddSingleton<INativeWorldFacts>(native));
		var guestWorld = w.G1.Services.GetRequiredService<IWorldControl>();
		var answers = new List<IReadOnlyList<BlockDamageEntryMsg>>();
		guestWorld.BlockDamageSnapshotReceived += entries => answers.Add(entries);

		SwallowGuestReport(w, guestWorld, 5, 7, SwallowedDamage);
		w.Driver.Tick(61_000);

		var row = Assert.Single(Assert.Single(answers));
		Assert.True(row.X == 5 && row.Y == 7 && row.Damage == 0f,
			$"a refused report must come back as an explicit zero, got {row.Damage}");
		Assert.Empty(native.Damages);

		// The zero answer still cleared the pending entry — a refused report must
		// converge, not re-report forever.
		w.Driver.Tick(61_000);
		Assert.Equal(1, MergeCalls(native));
		Assert.Equal(0, PendingDamageReports(w));
	}

	[Fact]
	public void AirWrite_ForgetsThePendingCell()
	{
		var native = new FakeNativeWorldFacts();
		using var w = ItemSimWorld.Create(services => services.AddSingleton<INativeWorldFacts>(native));
		var guestWorld = w.G1.Services.GetRequiredService<IWorldControl>();
		w.Driver.Network.SetFaults(w.G1.SteamId, w.Host.SteamId, new LinkFaults { Down = true });
		guestWorld.ReportBlockDamage(5, 7, SwallowedDamage);
		w.Driver.Network.ClearFaults(w.G1.SteamId, w.Host.SteamId);

		// The cell went air before the fallback fired (a break, local or remote):
		// its damage belongs to the block-state channel now.
		guestWorld.ForgetPendingBlockDamage(5, 7);
		w.Driver.Tick(61_000);

		Assert.Equal(0, MergeCalls(native));
		Assert.Equal(0, PendingDamageReports(w));
		Assert.Empty(native.Damages);
	}

	[Fact]
	public void WorldReset_DropsThePendingReports()
	{
		var native = new FakeNativeWorldFacts();
		using var w = ItemSimWorld.Create(services => services.AddSingleton<INativeWorldFacts>(native));
		var guestWorld = w.G1.Services.GetRequiredService<IWorldControl>();
		w.Driver.Network.SetFaults(w.G1.SteamId, w.Host.SteamId, new LinkFaults { Down = true });
		guestWorld.ReportBlockDamage(5, 7, SwallowedDamage);
		w.Driver.Network.ClearFaults(w.G1.SteamId, w.Host.SteamId);

		// A new world/layer baseline: the previous world's pending reports must
		// never be merged into the new one.
		guestWorld.ResetPendingBlockDamageReports();
		w.Driver.Tick(61_000);

		Assert.Equal(0, MergeCalls(native));
		Assert.Equal(0, PendingDamageReports(w));
		Assert.Empty(native.Damages);
	}

	[Fact]
	public void NoNativeReader_LeavesTheReportOutstandingInsteadOfInventingAnAnswer()
	{
		// A Runtime-only composition: no adapter registered the port, so there is
		// no table to merge into and no value to answer with.
		using var w = ItemSimWorld.Create();
		var guestWorld = w.G1.Services.GetRequiredService<IWorldControl>();
		SwallowGuestReport(w, guestWorld, 5, 7, SwallowedDamage);

		w.Driver.Tick(61_000);
		Assert.Equal(1, PendingDamageReports(w));

		// No answer came back, so the entry survives and the fallback keeps
		// carrying it rather than pretending it converged.
		w.Driver.Tick(61_000);
		Assert.Equal(1, PendingDamageReports(w));
		Assert.Equal(0, w.ReceivedCount(w.G1, NetMsg.BlockDamageSnapshot));
	}

	[Fact]
	public void HostRole_NeverRecordsAPendingReport()
	{
		using var w = ItemSimWorld.Create();
		var hostWorld = w.Host.Services.GetRequiredService<IWorldControl>();

		hostWorld.ReportBlockDamage(5, 7, SwallowedDamage);
		w.Driver.Tick(61_000);

		Assert.Equal(0, w.Host.Services.GetRequiredService<WorldService>().PendingBlockDamageReportCount);
	}

	[Fact]
	public void GuestRole_NeverMergesOrAnswersAReport()
	{
		var native = new FakeNativeWorldFacts();
		using var w = ItemSimWorld.Create(services => services.AddSingleton<INativeWorldFacts>(native));
		var guestWorld = w.G1.Services.GetRequiredService<IWorldControl>();

		guestWorld.HandleBlockDamageReport(w.Host.SteamId, [new BlockDamageEntryMsg { X = 5, Y = 7, Damage = 40f }]);
		w.Driver.Tick(33);

		Assert.Equal(0, MergeCalls(native));
		Assert.Empty(native.Damages);
	}
}
