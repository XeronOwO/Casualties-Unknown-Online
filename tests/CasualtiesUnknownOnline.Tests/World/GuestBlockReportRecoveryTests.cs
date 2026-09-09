using System.Collections.Generic;
using CasualtiesUnknownOnline.Runtime.Protocol;
using CasualtiesUnknownOnline.Runtime.Session.World;
using CasualtiesUnknownOnline.Tests.Fakes;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.World;

/// <summary>
/// Guest world-block report recovery (sync-coverage audit W1). The host→guest
/// direction heals a lost relay with the absolute block-state table, but a
/// swallowed guest→host report had no recovery: the host never learned the
/// cell, so its absolute table omitted it and could not heal either side. The
/// guest now keeps its unacknowledged reports in
/// <see cref="PendingBlockReportTable"/>, re-reports them on the 60 s fallback
/// cycle (<see cref="BlockReportFallbackPump"/> → WorldService), and drops each
/// entry when the host answers for that cell (relay echo or correction) or when
/// a new world/layer baseline is applied. The absolute snapshot and the
/// world-entry completion marker deliberately do NOT clear the table — a
/// reconnect-while-in-world keeps the guest's local mutations.
/// <para>
/// The host executor here is a contract double for the Game Adapter's thin
/// shell (the same pattern as BlockBreakSimulationTests): it answers every
/// report with the host's current cell value — the accepted relay includes the
/// reporter, a refused report gets a targeted correction. That answer is what
/// makes the pending table self-clearing; the real adapter's arbitration and
/// apply remain adapter-side and are covered by the unified acceptance pass.
/// </para>
/// </summary>
[Trait("Category", "Integration")]
public class GuestBlockReportRecoveryTests
{
	/// <summary>A block id the generated world holds at the test cells (any non-air id behaves the same).</summary>
	private const ushort HostBlock = 12;

	/// <summary>A recording-only host listener (the "silent host" of the reset tests — it never answers).</summary>
	private static List<(ulong Sender, int X, int Y, ushort Block)> RecordHostReports(ItemSimWorld w)
	{
		var reports = new List<(ulong Sender, int X, int Y, ushort Block)>();
		w.Host.Services.GetRequiredService<IWorldControl>().BlockPlacedReceived += (sender, x, y, block) =>
			reports.Add((sender, x, y, block));
		return reports;
	}

	/// <summary>
	/// The host executor contract double: record the report, arbitrate with the
	/// host's cell table, then answer the reporter (accepted relay to everyone
	/// including the reporter; refused report gets a targeted correction).
	/// </summary>
	private static (List<(ulong Sender, int X, int Y, ushort Block)> Reports, Dictionary<(int X, int Y), ushort> HostBlocks) InstallHostExecutor(ItemSimWorld w)
	{
		var reports = new List<(ulong Sender, int X, int Y, ushort Block)>();
		var hostBlocks = new Dictionary<(int X, int Y), ushort>();
		var hostWorld = w.Host.Services.GetRequiredService<IWorldControl>();
		hostWorld.BlockPlacedReceived += (sender, x, y, block) =>
		{
			reports.Add((sender, x, y, block));
			var current = hostBlocks.TryGetValue((x, y), out var value) ? value : (ushort)0;
			if ((block == 0) == (current == 0))
			{
				// First-writer-wins: the host's cell stands — answer the reporter with it.
				hostWorld.SendBlockPlacedCorrection(sender, x, y, current);
				return;
			}

			hostBlocks[(x, y)] = block;
			hostWorld.BroadcastBlockPlaced(0, x, y, block); // accepted: relay to everyone, the reporter included (its acknowledgement)
		};
		return (reports, hostBlocks);
	}

	[Fact]
	public void SwallowedGuestReport_IsReReportedOnTheFallbackCycleAndConverges()
	{
		using var w = ItemSimWorld.Create();
		var (reports, hostBlocks) = InstallHostExecutor(w);
		hostBlocks[(5, 7)] = HostBlock; // the host's world still holds the block
		var guestWorld = w.G1.Services.GetRequiredService<IWorldControl>();

		// The lazy-P2P window: the live report never lands.
		w.Driver.Network.SetFaults(w.G1.SteamId, w.Host.SteamId, new LinkFaults { Down = true });
		guestWorld.SendBlockPlacedReport(5, 7, 0);
		w.Driver.Tick(33);
		Assert.Empty(reports);

		// The link heals. Inside the 60 s window nothing is re-sent (the live
		// report just went out); past it the fallback re-reports the cell.
		w.Driver.Network.ClearFaults(w.G1.SteamId, w.Host.SteamId);
		w.Driver.Tick(59_000);
		Assert.Empty(reports);
		w.Driver.Tick(2_000);
		var report = Assert.Single(reports);
		Assert.Equal((w.G1.SteamId, 5, 7, (ushort)0), report);

		// The host adopted the break (air onto solid) and relayed it back to the
		// reporter — the echo clears the pending entry; the same relay reaches
		// the third member, so every peer converges.
		Assert.Equal((ushort)0, hostBlocks[(5, 7)]);
		Assert.True(w.ReceivedCount(w.G2, NetMsg.BlockPlaced) >= 1,
			$"the accepted relay must reach the other members, got {w.ReceivedCount(w.G2, NetMsg.BlockPlaced)}");
		w.Driver.Tick(61_000);
		Assert.Single(reports);
	}

	[Fact]
	public void RefusedReport_IsAnsweredWithTheHostsAuthoritativeValue()
	{
		using var w = ItemSimWorld.Create();
		var (reports, hostBlocks) = InstallHostExecutor(w);
		hostBlocks[(9, 9)] = HostBlock; // the host's world already holds a block there
		var guestWorld = w.G1.Services.GetRequiredService<IWorldControl>();
		var answers = new List<(int X, int Y, ushort Block)>();
		guestWorld.BlockPlacedReceived += (_, x, y, block) => answers.Add((x, y, block));

		// A placement the host must refuse (the cell is occupied) — first-writer-wins.
		guestWorld.SendBlockPlacedReport(9, 9, 7);
		w.Driver.Tick(33);

		var answer = Assert.Single(answers);
		Assert.Equal((9, 9, HostBlock), answer);
		Assert.Equal(HostBlock, hostBlocks[(9, 9)]);

		// The correction cleared the pending entry: no further re-report.
		w.Driver.Tick(61_000);
		Assert.Single(reports);
	}

	[Fact]
	public void WorldSnapshotComplete_DoesNotDropUnansweredReports()
	{
		using var w = ItemSimWorld.Create();
		var reports = RecordHostReports(w); // recording only — the host never answers here
		var guestWorld = w.G1.Services.GetRequiredService<IWorldControl>();

		w.Driver.Network.SetFaults(w.G1.SteamId, w.Host.SteamId, new LinkFaults { Down = true });
		guestWorld.SendBlockPlacedReport(5, 7, 0);
		w.Driver.Tick(33);

		// Prove the entry is outstanding: once the link heals the fallback
		// re-reports it (the silent host records but never answers).
		w.Driver.Network.ClearFaults(w.G1.SteamId, w.Host.SteamId);
		w.Driver.Tick(61_000);
		Assert.Single(reports);

		// The world-entry group's completion marker is NOT a world boundary: a
		// reconnect-while-in-world keeps the guest's local mutations, so an
		// unanswered report must stay pending and be re-reported.
		var markers = 0;
		guestWorld.WorldSnapshotCompleteReceived += () => markers++;
		w.Host.Services.GetRequiredService<IWorldControl>().SendWorldSnapshotComplete(w.G1.SteamId);
		w.Driver.Tick(33);
		Assert.Equal(1, markers);
		w.Driver.Tick(61_000);
		Assert.Equal(2, reports.Count);
	}

	[Fact]
	public void BackwardsClock_ReArmsInsteadOfStallingTheFallback()
	{
		using var w = ItemSimWorld.Create();
		var reports = RecordHostReports(w); // recording only — the host never answers here
		var guestWorld = w.G1.Services.GetRequiredService<IWorldControl>();

		w.Driver.Network.SetFaults(w.G1.SteamId, w.Host.SteamId, new LinkFaults { Down = true });
		guestWorld.SendBlockPlacedReport(5, 7, 0);
		w.Driver.Tick(33);
		w.Driver.Network.ClearFaults(w.G1.SteamId, w.Host.SteamId);

		// Environment.TickCount wraps to a negative int every ~24.9 days: the
		// fallback must re-arm at the new reading instead of waiting for the
		// counter to catch up with the pre-wrap anchor.
		w.Driver.Clock.Advance(-(w.Driver.NowMs + 1_000));
		w.Driver.Tick(33);
		w.Driver.Tick(61_000);

		Assert.Single(reports);
	}

	[Fact]
	public void ResetPendingBlockReports_ClearsThePreviousWorldsReports()
	{
		using var w = ItemSimWorld.Create();
		var reports = RecordHostReports(w); // recording only — the host never answers here
		var guestWorld = w.G1.Services.GetRequiredService<IWorldControl>();

		w.Driver.Network.SetFaults(w.G1.SteamId, w.Host.SteamId, new LinkFaults { Down = true });
		guestWorld.SendBlockPlacedReport(5, 7, 0);
		w.Driver.Tick(33);
		w.Driver.Network.ClearFaults(w.G1.SteamId, w.Host.SteamId);
		w.Driver.Tick(61_000);
		Assert.Single(reports);

		// The guest applied a new world/layer baseline (the adapter's
		// WorldParamsService calls this at the generation boundary): the previous
		// world's cells must not be arbitrated into the new one.
		guestWorld.ResetPendingBlockReports();
		w.Driver.Tick(61_000);
		Assert.Single(reports);
	}

	[Fact]
	public void SessionEnd_ClearsPendingReports()
	{
		using var w = ItemSimWorld.Create();
		_ = RecordHostReports(w); // recording only — the entry stays outstanding
		var guestWorld = w.G1.Services.GetRequiredService<WorldService>();
		guestWorld.SendBlockPlacedReport(5, 7, 0);
		w.Driver.Tick(33);
		Assert.Equal(1, guestWorld.PendingBlockReportCount);

		w.G1.Session.EndSession();

		Assert.Equal(0, guestWorld.PendingBlockReportCount);
	}
}
