using CasualtiesUnknownOnline.GameState;
using CasualtiesUnknownOnline.Runtime.Session.Items;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Items;

/// <summary>
/// The host's tombstones for refused item creations: a refusal is remembered
/// with its reason so a LATER operation on the same id can be answered with the
/// precise reason instead of looking like an id whose creation was never
/// reported at all (a protocol violation). Bounded — oldest evicted first — and
/// cleared when the session ends, because item ids are session-partitioned.
/// </summary>
public class RefusedItemCreationsTests
{
	[Fact]
	public void ARecordedRefusal_IsRememberedWithItsReason()
	{
		var table = new RefusedItemCreations();

		table.Record(42, RejectionReason.BlockAlreadyBroken);

		Assert.True(table.TryGet(42, out var reason));
		Assert.Equal(RejectionReason.BlockAlreadyBroken, reason);
		Assert.False(table.TryGet(43, out _), "an id that was never refused has no tombstone");
	}

	[Fact]
	public void ReRecordingAnId_ReplacesTheReason_WithoutGrowingTheTable()
	{
		var table = new RefusedItemCreations();
		table.Record(42, RejectionReason.UnknownAggregate);

		table.Record(42, RejectionReason.BlockAlreadyBroken);

		Assert.Equal(1, table.Count);
		Assert.True(table.TryGet(42, out var reason));
		Assert.Equal(RejectionReason.BlockAlreadyBroken, reason);
	}

	[Fact]
	public void TheTableIsBounded_TheOldestRefusalIsEvictedFirst()
	{
		var table = new RefusedItemCreations();
		for (ulong id = 1; id <= (ulong)RefusedItemCreations.Capacity + 1; id++)
		{
			table.Record(id, RejectionReason.UnknownAggregate);
		}

		Assert.Equal(RefusedItemCreations.Capacity, table.Count);
		Assert.False(table.TryGet(1, out _), "the oldest refusal is the one evicted");
		Assert.True(table.TryGet((ulong)RefusedItemCreations.Capacity + 1, out _));
	}

	[Fact]
	public void AnIdLessReport_IsNeverRemembered_AndTheSessionEndForgetsTheRest()
	{
		var table = new RefusedItemCreations();
		table.Record(0, RejectionReason.BlockAlreadyBroken);
		Assert.Equal(0, table.Count);

		table.Record(42, RejectionReason.BlockAlreadyBroken);
		table.Reset();

		Assert.Equal(0, table.Count);
		Assert.False(table.TryGet(42, out _));
	}
}
