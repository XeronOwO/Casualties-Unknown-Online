using CasualtiesUnknownOnline.Runtime.Session.World;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.World;

/// <summary>
/// The two tables the per-sender partial-damage accounting is built from
/// (review/partial-damage-delta-report-overlap): the GUEST's own contribution per
/// cell (<see cref="LocalBlockDamageContributions"/>) and the HOST's per-sender
/// ledger (<see cref="RemoteBlockDamageLedger"/>). Their rules are what make a
/// contribution count exactly once — a repeat resolves to no increment, a stale
/// frame cannot lower the ledger, an answer keeps the cumulative value, and a
/// block write forgets the cell on both sides.
/// </summary>
public class PartialDamageLedgerTests
{
	// ---- the guest's own contribution ----

	[Fact]
	public void Add_AccumulatesAndReturnsTheCumulativeValue()
	{
		var table = new LocalBlockDamageContributions();

		Assert.Equal(20f, table.Add(5, 7, 20f));
		Assert.Equal(25f, table.Add(5, 7, 5f));
		Assert.Equal(1, table.Count);
		Assert.Equal(1, table.TrackedCount);

		var entry = Assert.Single(table.Outstanding);
		Assert.True(entry.X == 5 && entry.Y == 7 && entry.Damage == 25f, $"the report carries the cumulative value, got {entry.Damage}");
	}

	[Fact]
	public void Answer_ClearsOnlyTheOutstandingFlagAndKeepsTheCumulativeValue()
	{
		var table = new LocalBlockDamageContributions();
		table.Add(5, 7, 20f);

		Assert.True(table.Answer(5, 7));
		Assert.Equal(0, table.Count);
		Assert.Equal(1, table.TrackedCount);
		Assert.Empty(table.Outstanding);

		// A later hit reports cumulative + its increment: the host resolves the
		// difference against the ledger it already holds.
		Assert.Equal(25f, table.Add(5, 7, 5f));
		Assert.Equal(1, table.Count);
		Assert.False(table.Answer(5, 8)); // an untracked cell answers nothing
	}

	[Fact]
	public void Forget_RemovesTheCellSoAFreshBlockStartsFromZero()
	{
		var table = new LocalBlockDamageContributions();
		table.Add(5, 7, 20f);

		Assert.True(table.Forget(5, 7));
		Assert.False(table.Forget(5, 7));
		Assert.Equal(0, table.TrackedCount);

		Assert.Equal(4f, table.Add(5, 7, 4f));
	}

	[Fact]
	public void Clear_DropsEveryCell()
	{
		var table = new LocalBlockDamageContributions();
		table.Add(5, 7, 20f);
		table.Add(6, 7, 30f);

		table.Clear();

		Assert.Equal(0, table.Count);
		Assert.Equal(0, table.TrackedCount);
		Assert.Empty(table.Outstanding);
	}

	[Fact]
	public void Add_AtTheCap_RefusesANewCellButStillAccumulatesATrackedOne()
	{
		var table = new LocalBlockDamageContributions(cap: 1);
		Assert.Equal(20f, table.Add(5, 7, 20f));

		// A new cell cannot be tracked: the caller sends no contribution for it, so
		// the host falls back to the raw delta (the bounded degradation).
		Assert.Equal(0f, table.Add(6, 7, 30f));
		Assert.Equal(1, table.TrackedCount);

		// A tracked cell always accumulates.
		Assert.Equal(25f, table.Add(5, 7, 5f));
	}

	[Fact]
	public void Add_WithANonPositiveIncrement_CreatesNothing()
	{
		var table = new LocalBlockDamageContributions();

		Assert.Equal(0f, table.Add(5, 7, 0f));
		Assert.Equal(0f, table.Add(5, 7, -1f));
		Assert.Equal(0, table.TrackedCount);
	}

	// ---- the host's per-sender ledger ----

	[Fact]
	public void Resolve_OfAnUntrackedCell_IsAllNewAndCommits()
	{
		var ledger = new RemoteBlockDamageLedger();

		var resolution = ledger.Resolve(sender: 11UL, x: 5, y: 7, contribution: 20f);

		Assert.True(resolution.Trackable);
		Assert.Equal(20f, resolution.Increment);
		ledger.Commit(resolution);
		Assert.Equal(1, ledger.Count);
	}

	[Fact]
	public void Resolve_OfATrackedCell_IsOnlyTheDifference()
	{
		var ledger = new RemoteBlockDamageLedger();
		ledger.Commit(ledger.Resolve(11UL, 5, 7, 20f));

		var next = ledger.Resolve(11UL, 5, 7, 50f);

		Assert.Equal(30f, next.Increment);
	}

	[Fact]
	public void Resolve_OfARepeatOrAStaleFrame_HasNoIncrement()
	{
		var ledger = new RemoteBlockDamageLedger();
		ledger.Commit(ledger.Resolve(11UL, 5, 7, 20f));

		// The duplicate report and the retransmitted delta are the same case — and
		// so is a frame whose value is below what the ledger already holds.
		Assert.Equal(0f, ledger.Resolve(11UL, 5, 7, 20f).Increment);
		Assert.Equal(0f, ledger.Resolve(11UL, 5, 7, 12f).Increment);
	}

	[Fact]
	public void Commit_IsMonotone_SoAStaleReportCannotMakeTheNextIncrementTooLarge()
	{
		var ledger = new RemoteBlockDamageLedger();
		ledger.Commit(ledger.Resolve(11UL, 5, 7, 40f));

		ledger.Commit(ledger.Resolve(11UL, 5, 7, 10f)); // a late, lower report

		Assert.Equal(10f, ledger.Resolve(11UL, 5, 7, 50f).Increment);
	}

	[Fact]
	public void Resolve_TracksEverySenderSeparately()
	{
		var ledger = new RemoteBlockDamageLedger();
		ledger.Commit(ledger.Resolve(11UL, 5, 7, 20f));

		// The other sender's contribution is entirely new — this is what makes two
		// senders ADD UP instead of the lower one being swallowed by a maximum.
		var other = ledger.Resolve(22UL, 5, 7, 30f);

		Assert.Equal(30f, other.Increment);
		ledger.Commit(other);
		Assert.Equal(2, ledger.Count);
	}

	[Fact]
	public void Forget_DropsEverySendersEntryForThatCellOnly()
	{
		var ledger = new RemoteBlockDamageLedger();
		ledger.Commit(ledger.Resolve(11UL, 5, 7, 20f));
		ledger.Commit(ledger.Resolve(22UL, 5, 7, 30f));
		ledger.Commit(ledger.Resolve(11UL, 6, 8, 40f));

		Assert.Equal(2, ledger.Forget(5, 7));
		Assert.Equal(1, ledger.Count);
		Assert.Equal(0, ledger.Forget(5, 7));

		// The forgotten cell starts from zero again — a fresh block at the same cell
		// must not inherit the old block's accounting.
		Assert.Equal(5f, ledger.Resolve(11UL, 5, 7, 5f).Increment);
	}

	[Fact]
	public void Clear_DropsEveryEntry()
	{
		var ledger = new RemoteBlockDamageLedger();
		ledger.Commit(ledger.Resolve(11UL, 5, 7, 20f));

		ledger.Clear();

		Assert.Equal(0, ledger.Count);
	}

	[Fact]
	public void Resolve_AtTheCap_IsNotTrackableButStillYieldsTheWholeContribution()
	{
		var ledger = new RemoteBlockDamageLedger(cap: 1);
		ledger.Commit(ledger.Resolve(11UL, 5, 7, 20f));

		var overflow = ledger.Resolve(22UL, 6, 8, 30f);

		Assert.False(overflow.Trackable);
		Assert.Equal(30f, overflow.Increment); // the damage still flows: a full ledger must not LOSE a sender's contribution
		ledger.Commit(overflow);
		Assert.Equal(1, ledger.Count);
	}
}
