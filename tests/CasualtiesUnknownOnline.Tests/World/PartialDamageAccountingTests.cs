using System.Linq;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using CasualtiesUnknownOnline.Runtime.Session.World;
using CasualtiesUnknownOnline.Tests.Fakes;
using CasualtiesUnknownOnline.Tests.Persistence;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.World;

/// <summary>
/// Partial block damage is counted exactly once per sender whatever interleaving
/// the transport produces (review/partial-damage-delta-report-overlap, the
/// limitation the W2 landing recorded). The live relay is a DELTA the receiver
/// ACCUMULATES and the recovery report carries the sender's cumulative
/// CONTRIBUTION, so both arrival paths resolve against the host's per-sender
/// ledger. Before the ledger existed the two halves disagreed in both directions:
/// <list type="bullet">
/// <item>UNDER-count: two senders whose reports both arrived converged to the
/// higher value instead of the sum.</item>
/// <item>OVER-count: a delta landing AFTER the report covering it raised the row
/// a second time.</item>
/// </list>
/// <para>
/// The native damage list is faked at the <see cref="INativeWorldFacts"/> seam and
/// the ADAPTER's half of the live path — the additive apply of an increment into
/// that list, which in production is <c>WorldGeneration.DamageBlock</c>'s
/// <c>blockDamage.damage += dmg</c> reached from
/// <c>BlockBreakSync.OnRemoteBlockDamaged</c> — is modelled by a subscriber on
/// <see cref="IWorldControl.BlockDamagedReceived"/>. That keeps the whole
/// Runtime-owned decision (what a report and a delta are worth) under test; the
/// engine-side read/write of the adapter stays code-reviewed plus the dual-client
/// pass, exactly as the W2 landing recorded.
/// </para>
/// </summary>
[Trait("Category", "Integration")]
public class PartialDamageAccountingTests
{
	private const int CellX = 5;
	private const int CellY = 7;

	/// <summary>This host's accumulated damage for the cell — the game's own list as the fake holds it.</summary>
	private static float Row(FakeNativeWorldFacts native)
	{
		var row = native.Damages.FirstOrDefault(d => d.X == CellX && d.Y == CellY);
		return row?.Damage ?? 0f;
	}

	/// <summary>
	/// The adapter's half of the live path, in the harness's own terms: the
	/// increment this host resolved is applied ADDITIVELY to the game's own list
	/// (the game's own <c>DamageBlock</c> body does <c>blockDamage.damage += dmg</c>),
	/// and only while the report belongs to this world.
	/// </summary>
	private static void ModelAdapterRelay(ItemSimWorld w, FakeNativeWorldFacts native) =>
		w.Host.Services.GetRequiredService<IWorldControl>().BlockDamagedReceived +=
			(sender, cellX, cellY, damage, metalBonus, drops, buildingDrops, relation) =>
			{
				if (relation == WorldGenerationRelation.Stale || drops is { Count: > 0 })
				{
					return;
				}

				native.ApplyDamage(cellX, cellY, damage);
			};

	/// <summary>One sender's live delta, carrying that sender's cumulative contribution for the cell (0 = the raw fallback the ledger cannot account for).</summary>
	private static void LandDelta(ItemSimWorld w, ulong sender, float damage, float contribution)
	{
		w.Host.Services.GetRequiredService<IWorldControl>().FireBlockDamagedReceived(
			sender,
			CellX,
			CellY,
			damage,
			false,
			null,
			null,
			contribution,
			WorldGenerationReports.StampOf(w.Host));
		w.Driver.Tick(33);
	}

	/// <summary>One sender's report of its own cumulative contribution for the cell.</summary>
	private static void Report(ItemSimWorld w, ulong sender, float contribution)
	{
		w.Host.Services.GetRequiredService<IWorldControl>().HandleBlockDamageReport(
			sender,
			[new BlockDamageEntryMsg { X = CellX, Y = CellY, Damage = contribution }],
			generation: null);
		w.Driver.Tick(33);
	}

	[Fact]
	public void TwoSenders_ContributionsAddUpInsteadOfTakingTheMaximum()
	{
		var native = new FakeNativeWorldFacts();
		using var w = ItemSimWorld.Create(services => services.AddSingleton<INativeWorldFacts>(native));
		ModelAdapterRelay(w, native);

		// Two guests damage the same cell, each from its own view (neither saw the
		// other's hit), and both live delta reports are swallowed: only the
		// fallback's outstanding set ever reaches the host.
		w.G1.Services.GetRequiredService<IWorldControl>().AddLocalBlockDamage(CellX, CellY, 20f);
		w.G2.Services.GetRequiredService<IWorldControl>().AddLocalBlockDamage(CellX, CellY, 30f);

		w.Driver.Tick(33); // the fallback's window arms on the first frame with work
		w.Driver.Tick(61_000);

		Assert.True(Row(native) == 50f,
			$"two senders' contributions must sum on the host's row, it ended at {Row(native)}");
	}

	[Fact]
	public void DelayedDelta_AfterTheAbsoluteReport_IsNotAppliedTwice()
	{
		var native = new FakeNativeWorldFacts();
		using var w = ItemSimWorld.Create(services => services.AddSingleton<INativeWorldFacts>(native));
		ModelAdapterRelay(w, native);
		WorldGenerationReports.CommitRun(w.Host, layerIndex: 1);
		var guestWorld = w.G1.Services.GetRequiredService<IWorldControl>();

		// The guest's hit applied locally and recorded its contribution; its live
		// delta was swallowed, so the fallback's report is what reaches the host.
		var cumulative = guestWorld.AddLocalBlockDamage(CellX, CellY, 20f);
		Assert.Equal(20f, cumulative);
		w.Driver.Tick(33);
		w.Driver.Tick(61_000);

		Assert.True(Row(native) == 20f,
			$"the report must land the guest's contribution once, the row ended at {Row(native)}");

		// The retransmitted live delta lands AFTER the report that already covers
		// it: it carries the same cumulative contribution, so the ledger resolves no
		// increment and the row must not move.
		LandDelta(w, w.G1.SteamId, 20f, cumulative);

		Assert.True(Row(native) == 20f,
			$"a delayed delta must not add a second time, the row ended at {Row(native)}");
	}

	[Fact]
	public void DeltaThenReport_CountsTheContributionOnce()
	{
		var native = new FakeNativeWorldFacts();
		using var w = ItemSimWorld.Create(services => services.AddSingleton<INativeWorldFacts>(native));
		ModelAdapterRelay(w, native);
		WorldGenerationReports.CommitRun(w.Host, layerIndex: 1);
		var guestWorld = w.G1.Services.GetRequiredService<IWorldControl>();

		var cumulative = guestWorld.AddLocalBlockDamage(CellX, CellY, 20f);
		LandDelta(w, w.G1.SteamId, 20f, cumulative);

		Assert.True(Row(native) == 20f, $"the live delta lands once, the row ended at {Row(native)}");

		// The unaffected half: the contribution is still unaccounted for on this
		// side, so the fallback re-reports it — and the host must not count it twice.
		w.Driver.Tick(61_000);
		Assert.True(Row(native) == 20f,
			$"the report of an already-counted contribution must change nothing, the row ended at {Row(native)}");
		Assert.Equal(0, w.G1.Services.GetRequiredService<WorldService>().PendingBlockDamageReportCount);
	}

	[Fact]
	public void DuplicateAbsoluteReport_IsIdempotent()
	{
		var native = new FakeNativeWorldFacts();
		using var w = ItemSimWorld.Create(services => services.AddSingleton<INativeWorldFacts>(native));
		ModelAdapterRelay(w, native);
		WorldGenerationReports.CommitRun(w.Host, layerIndex: 1);

		Report(w, w.G1.SteamId, 20f);
		Assert.True(Row(native) == 20f, $"the first report lands, the row ended at {Row(native)}");

		Report(w, w.G1.SteamId, 20f);
		Assert.True(Row(native) == 20f,
			$"a duplicate report must be idempotent, the row ended at {Row(native)}");
	}

	[Fact]
	public void OneSendersLaterHit_RaisesTheRowByItsIncrementOnly()
	{
		var native = new FakeNativeWorldFacts();
		using var w = ItemSimWorld.Create(services => services.AddSingleton<INativeWorldFacts>(native));
		ModelAdapterRelay(w, native);
		WorldGenerationReports.CommitRun(w.Host, layerIndex: 1);
		var guestWorld = w.G1.Services.GetRequiredService<IWorldControl>();

		// First hit: reported (so the ledger holds 20) but its answer was lost.
		guestWorld.AddLocalBlockDamage(CellX, CellY, 20f);
		Report(w, w.G1.SteamId, 20f);
		Assert.Equal(20f, Row(native));

		// A second hit at the same cell: the contribution is cumulative, so only
		// the difference is new to this host.
		var cumulative = guestWorld.AddLocalBlockDamage(CellX, CellY, 5f);
		Assert.Equal(25f, cumulative);
		Report(w, w.G1.SteamId, cumulative);

		Assert.True(Row(native) == 25f,
			$"only the new increment may be applied, the row ended at {Row(native)}");
	}

	[Fact]
	public void TwoSenders_BothInterleavings_LandTheSameSum()
	{
		var native = new FakeNativeWorldFacts();
		using var w = ItemSimWorld.Create(services => services.AddSingleton<INativeWorldFacts>(native));
		ModelAdapterRelay(w, native);
		WorldGenerationReports.CommitRun(w.Host, layerIndex: 1);

		// Sender 1: report first, then its delayed delta.
		Report(w, w.G1.SteamId, 20f);
		LandDelta(w, w.G1.SteamId, 20f, 20f);

		// Sender 2: the other interleaving — delta first, then the report.
		LandDelta(w, w.G2.SteamId, 30f, 30f);
		Report(w, w.G2.SteamId, 30f);

		Assert.True(Row(native) == 50f,
			$"both senders must be counted exactly once, the row ended at {Row(native)}");
	}

	[Fact]
	public void RawDelta_WithoutAContribution_KeepsThePreLedgerBehaviour()
	{
		// A sender that could not account for the cell (its table is full, or the
		// message is a break payload's damage) reports no contribution: the host
		// then applies the raw damage additively, as it did before the ledger.
		var native = new FakeNativeWorldFacts();
		using var w = ItemSimWorld.Create(services => services.AddSingleton<INativeWorldFacts>(native));
		ModelAdapterRelay(w, native);
		WorldGenerationReports.CommitRun(w.Host, layerIndex: 1);

		LandDelta(w, w.G1.SteamId, 15f, 0f);
		LandDelta(w, w.G1.SteamId, 15f, 0f);

		Assert.True(Row(native) == 30f,
			$"a raw delta keeps its additive semantics, the row ended at {Row(native)}");
	}
}
