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
/// The guest partial-damage contribution's recovery (sync-coverage audit W2,
/// accounted per sender since review/partial-damage-delta-report-overlap). The live
/// report is a DELTA and the host's authoritative table is the GAME's own
/// <c>blockDamages</c> list, so a swallowed report leaves the host short by
/// exactly that hit. The guest keeps the cells' own cumulative CONTRIBUTION
/// (<see cref="LocalBlockDamageContributions"/>), re-reports the outstanding ones
/// on the fallback cycle, and stops reporting a cell when the host ANSWERS for it
/// — the answer, never the periodic snapshot, is what says "this host accounted
/// for your report". The cumulative value survives the answer, so a later hit at
/// the same cell reports cumulative + its increment and the host resolves the
/// difference against its ledger.
/// <para>
/// The native damage table is faked at the <see cref="INativeWorldFacts"/> seam,
/// and the adapter's own half of the live path — the additive apply into the
/// game's list and the relay of what was applied — is modelled by a subscriber on
/// the host's <see cref="IWorldControl.BlockDamagedReceived"/>. What this suite
/// does NOT execute is the engine-side read/write itself (<c>DamageBlock</c>'s
/// accumulation, the crack sprite, the break handling) and the patch that
/// measures a hit's applied increment; those rest on code review plus the unified
/// dual-client acceptance pass, and the ticket says so.
/// </para>
/// </summary>
[Trait("Category", "Integration")]
public class GuestBlockDamageReportRecoveryTests
{
	private const int CellX = 5;
	private const int CellY = 7;
	private const float Contribution = 20f;

	/// <summary>This host's accumulated damage for the cell — the game's own list as the fake holds it.</summary>
	private static float Row(FakeNativeWorldFacts native)
	{
		var row = native.Damages.FirstOrDefault(d => d.X == CellX && d.Y == CellY);
		return row?.Damage ?? 0f;
	}

	/// <summary>
	/// The adapter's half of the live path, in the harness's own terms: the
	/// increment this host was handed is ACCUMULATED into the game's own list and
	/// relayed to the other members (production: <c>BlockBreakSync</c> →
	/// <c>WorldGeneration.DamageBlock</c> → <c>BroadcastBlockDamaged</c>). Only the
	/// HOST's half is modelled: the fake it writes into is shared by the harness's
	/// nodes, so a guest-side model would double-apply into the same rows.
	/// </summary>
	private static void ModelHostDamageApplication(ItemSimWorld w, FakeNativeWorldFacts native)
	{
		var hostWorld = w.Host.Services.GetRequiredService<IWorldControl>();
		hostWorld.BlockDamagedReceived += (sender, cellX, cellY, damage, metalBonus, drops, buildingDrops, relation) =>
		{
			if (relation == WorldGenerationRelation.Stale || damage <= 0f || drops is { Count: > 0 })
			{
				return;
			}

			native.ApplyDamage(cellX, cellY, damage);
			hostWorld.BroadcastBlockDamaged(sender, cellX, cellY, damage, false, null, null);
		};
	}

	/// <summary>The lazy-P2P window: the guest applied the damage and recorded its contribution, but the live delta report never lands.</summary>
	private static void SwallowGuestReport(ItemSimWorld w, IWorldControl guestWorld, float contribution)
	{
		w.Driver.Network.SetFaults(w.G1.SteamId, w.Host.SteamId, new LinkFaults { Down = true });
		var cumulative = guestWorld.AddLocalBlockDamage(CellX, CellY, contribution);
		guestWorld.SendBlockDamaged(CellX, CellY, contribution, false, cumulative, null, null);
		w.Driver.Tick(33);

		// The link heals before the fallback window elapses.
		w.Driver.Network.ClearFaults(w.G1.SteamId, w.Host.SteamId);
	}

	/// <summary>The guest's outstanding-contribution count (the fallback pump's work check).</summary>
	private static int OutstandingContributions(ItemSimWorld w) =>
		w.G1.Services.GetRequiredService<WorldService>().PendingBlockDamageReportCount;

	[Fact]
	public void SwallowedPartialDamageReport_ConvergesOnTheFallbackCycle()
	{
		var native = new FakeNativeWorldFacts();
		using var w = ItemSimWorld.Create(services => services.AddSingleton<INativeWorldFacts>(native));
		ModelHostDamageApplication(w, native);
		var guestWorld = w.G1.Services.GetRequiredService<IWorldControl>();

		SwallowGuestReport(w, guestWorld, Contribution);

		Assert.Equal(0f, Row(native));

		// Inside the 60 s window nothing is re-sent (the live report just went
		// out); past it the fallback hands the host the guest's own contribution.
		w.Driver.Tick(59_000);
		Assert.Equal(0f, Row(native));
		w.Driver.Tick(2_000);

		Assert.Equal(Contribution, Row(native));
	}

	[Fact]
	public void HostsAnswer_ClearsTheOutstandingContribution()
	{
		var native = new FakeNativeWorldFacts();
		using var w = ItemSimWorld.Create(services => services.AddSingleton<INativeWorldFacts>(native));
		ModelHostDamageApplication(w, native);
		var guestWorld = w.G1.Services.GetRequiredService<IWorldControl>();

		SwallowGuestReport(w, guestWorld, Contribution);
		w.Driver.Tick(61_000);

		Assert.Equal(Contribution, Row(native));
		Assert.Equal(0, OutstandingContributions(w));
		Assert.Equal(1, w.ReceivedCount(w.G1, NetMsg.BlockDamageSnapshot)); // the answer to this side's report

		// The answer accounted for the cell: the next window re-sends nothing.
		w.Driver.Tick(61_000);
		Assert.Equal(1, w.ReceivedCount(w.G1, NetMsg.BlockDamageSnapshot));
	}

	[Fact]
	public void ThirdParty_ReceivesTheAppliedIncrement()
	{
		var native = new FakeNativeWorldFacts();
		using var w = ItemSimWorld.Create(services => services.AddSingleton<INativeWorldFacts>(native));
		ModelHostDamageApplication(w, native);
		var guestWorld = w.G1.Services.GetRequiredService<IWorldControl>();
		var relays = new List<(int X, int Y, float Damage)>();
		w.G2.Services.GetRequiredService<IWorldControl>().BlockDamagedReceived +=
			(_, x, y, damage, _, _, _, _) => relays.Add((x, y, damage));

		SwallowGuestReport(w, guestWorld, Contribution);
		w.Driver.Tick(61_000);

		var relay = Assert.Single(relays);
		Assert.Equal((CellX, CellY, Contribution), relay);
	}

	[Fact]
	public void PeriodicSnapshot_DoesNotClearAnotherSendersOutstandingContribution()
	{
		// The under-count's root cause: the periodic snapshot is authoritative
		// STATE, and clearing a third party's outstanding entry with it would drop
		// that party's contribution without it ever being reported.
		var native = new FakeNativeWorldFacts();
		using var w = ItemSimWorld.Create(services => services.AddSingleton<INativeWorldFacts>(native));
		ModelHostDamageApplication(w, native);
		var guestWorld = w.G1.Services.GetRequiredService<IWorldControl>();

		w.Driver.Network.SetFaults(w.G1.SteamId, w.Host.SteamId, new LinkFaults { Down = true });
		guestWorld.AddLocalBlockDamage(CellX, CellY, Contribution);
		w.Driver.Network.ClearFaults(w.G1.SteamId, w.Host.SteamId);

		// The host holds damage for the very same cell (another sender's), and its
		// periodic snapshot names it.
		native.SeedBlockDamage(CellX, CellY, 35f);
		w.Driver.Tick(61_000);

		Assert.Equal(1, OutstandingContributions(w));
		Assert.Equal(35f, Row(native)); // the state snapshot did not make this side's report count

		// The outstanding contribution still goes out on its own cycle — and the
		// host adds it to what it already held.
		w.Driver.Tick(61_000);
		Assert.Equal(35f + Contribution, Row(native));
		Assert.Equal(0, OutstandingContributions(w));
	}

	[Fact]
	public void AnsweredZero_ClearsTheOutstandingContribution()
	{
		// This host applied nothing (no live world means no application), so its
		// answer is an explicit zero for the reported cell — and that zero still
		// accounts for the report: it must not be re-sent forever.
		var native = new FakeNativeWorldFacts();
		using var w = ItemSimWorld.Create(services => services.AddSingleton<INativeWorldFacts>(native));
		var guestWorld = w.G1.Services.GetRequiredService<IWorldControl>();
		var answers = new List<IReadOnlyList<BlockDamageEntryMsg>>();
		guestWorld.BlockDamageSnapshotReceived += entries => answers.Add(entries);

		SwallowGuestReport(w, guestWorld, Contribution);
		w.Driver.Tick(61_000);

		var row = Assert.Single(Assert.Single(answers));
		Assert.True(row.X == CellX && row.Y == CellY && row.Damage == 0f,
			$"a report this host cannot account for must come back as an explicit zero, got {row.Damage}");
		Assert.Empty(native.Damages);
		Assert.Equal(0, OutstandingContributions(w));

		w.Driver.Tick(61_000);
		Assert.Single(answers); // answered once — the entry is not re-reported
	}

	[Fact]
	public void AirWrite_ForgetsTheOutstandingCell()
	{
		var native = new FakeNativeWorldFacts();
		using var w = ItemSimWorld.Create(services => services.AddSingleton<INativeWorldFacts>(native));
		ModelHostDamageApplication(w, native);
		var guestWorld = w.G1.Services.GetRequiredService<IWorldControl>();
		w.Driver.Network.SetFaults(w.G1.SteamId, w.Host.SteamId, new LinkFaults { Down = true });
		guestWorld.AddLocalBlockDamage(CellX, CellY, Contribution);
		w.Driver.Network.ClearFaults(w.G1.SteamId, w.Host.SteamId);

		// A block write landed on the cell before the fallback fired (a break, a
		// placement): the contribution belonged to the block that is gone.
		guestWorld.ForgetBlockDamageAccounting(CellX, CellY);
		w.Driver.Tick(61_000);

		Assert.Equal(0, OutstandingContributions(w));
		Assert.Empty(native.Damages);
	}

	[Fact]
	public void WorldReset_DropsTheOutstandingContributions()
	{
		var native = new FakeNativeWorldFacts();
		using var w = ItemSimWorld.Create(services => services.AddSingleton<INativeWorldFacts>(native));
		ModelHostDamageApplication(w, native);
		var guestWorld = w.G1.Services.GetRequiredService<IWorldControl>();
		w.Driver.Network.SetFaults(w.G1.SteamId, w.Host.SteamId, new LinkFaults { Down = true });
		guestWorld.AddLocalBlockDamage(CellX, CellY, Contribution);
		w.Driver.Network.ClearFaults(w.G1.SteamId, w.Host.SteamId);

		// A new world/layer baseline: the previous world's contributions must
		// never be merged into the new one.
		guestWorld.ResetPendingBlockDamageReports();
		w.Driver.Tick(61_000);

		Assert.Equal(0, OutstandingContributions(w));
		Assert.Empty(native.Damages);
	}

	[Fact]
	public void NoNativeReader_LeavesTheReportOutstandingInsteadOfInventingAnAnswer()
	{
		// A Runtime-only composition: no adapter registered the port, so there is
		// no table to read this host's own values from and nothing to answer with.
		using var w = ItemSimWorld.Create();
		var guestWorld = w.G1.Services.GetRequiredService<IWorldControl>();
		SwallowGuestReport(w, guestWorld, Contribution);

		w.Driver.Tick(61_000);
		Assert.Equal(1, OutstandingContributions(w));

		// No answer came back, so the entry survives and the fallback keeps
		// carrying it rather than pretending it converged.
		w.Driver.Tick(61_000);
		Assert.Equal(1, OutstandingContributions(w));
		Assert.Equal(0, w.ReceivedCount(w.G1, NetMsg.BlockDamageSnapshot));
	}

	[Fact]
	public void ReportFromAnotherGeneration_IsNeitherAppliedNorAnswered()
	{
		// Acceptance matrix row 4 of review/world-layer-generation-identity: a stale
		// row set for a regenerated cell must not be applied. The host is at layer 3
		// and the report says layer 2, so its cells address a world this side no
		// longer simulates: neither the application nor the answer runs (an answer
		// would write THIS generation's values into the reporter's older world).
		var native = new FakeNativeWorldFacts();
		using var w = ItemSimWorld.Create(services => services.AddSingleton<INativeWorldFacts>(native));
		ModelHostDamageApplication(w, native);
		WorldGenerationReports.CommitRun(w.Host, layerIndex: 3);
		var hostWorld = w.Host.Services.GetRequiredService<IWorldControl>();
		var guestWorld = w.G1.Services.GetRequiredService<IWorldControl>();

		guestWorld.AddLocalBlockDamage(CellX, CellY, Contribution); // this side holds the cell outstanding
		hostWorld.HandleBlockDamageReport(
			w.G1.SteamId,
			[new BlockDamageEntryMsg { X = CellX, Y = CellY, Damage = Contribution }],
			WorldGenerationReports.StampOf(w.Host, layerOverride: 2));
		w.Driver.Tick(33);

		Assert.Empty(native.Damages);
		Assert.Equal(0, w.ReceivedCount(w.G1, NetMsg.BlockDamageSnapshot));
		Assert.Equal(1, OutstandingContributions(w)); // the entry is the reporter's own boundary's business
	}

	[Fact]
	public void HostRole_NeverRecordsAContribution()
	{
		using var w = ItemSimWorld.Create();
		var hostWorld = w.Host.Services.GetRequiredService<IWorldControl>();

		Assert.Equal(0f, hostWorld.AddLocalBlockDamage(CellX, CellY, Contribution));
		w.Driver.Tick(61_000);

		Assert.Equal(0, w.Host.Services.GetRequiredService<WorldService>().PendingBlockDamageReportCount);
	}

	[Fact]
	public void GuestRole_NeverAccountsAReport()
	{
		var native = new FakeNativeWorldFacts();
		using var w = ItemSimWorld.Create(services => services.AddSingleton<INativeWorldFacts>(native));
		ModelHostDamageApplication(w, native);
		var guestWorld = w.G1.Services.GetRequiredService<IWorldControl>();

		guestWorld.HandleBlockDamageReport(w.Host.SteamId, [new BlockDamageEntryMsg { X = CellX, Y = CellY, Damage = 40f }], generation: null);
		w.Driver.Tick(33);

		Assert.Empty(native.Damages);
	}
}
