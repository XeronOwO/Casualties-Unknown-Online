using System.Linq;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using CasualtiesUnknownOnline.Runtime.Session.Items;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Items;

/// <summary>
/// The pure pending-pickup queue semantics: duplicate enqueue, first-writer
/// ordering, predicate extraction (container-content resolution), bounded
/// expiry and reset.
/// </summary>
public class PendingPickupQueueTests
{
	private static CharacterItemMsg Evidence(float condition) => new() { ItemId = "test_item", Condition = condition };

	[Fact]
	public void Enqueue_ReturnsFalseForTheSameSenderAndItem()
	{
		var q = new PendingPickupQueue();
		Assert.True(q.TryEnqueue(1, 42, Evidence(1f), nowMs: 0));
		Assert.False(q.TryEnqueue(1, 42, Evidence(1f), nowMs: 10), "the retransmission must be silent, not a second claim");
		Assert.True(q.Count == 1);
	}

	[Fact]
	public void Enqueue_AllowsDifferentSendersForTheSameItem()
	{
		var q = new PendingPickupQueue();
		Assert.True(q.TryEnqueue(1, 42, Evidence(1f), nowMs: 0));
		Assert.True(q.TryEnqueue(2, 42, Evidence(1f), nowMs: 0), "two guests may race the same unregistered item");
		Assert.True(q.Count == 2);
	}

	[Fact]
	public void TryTakeFirst_ReturnsQueueOrder_FirstWriterWins()
	{
		var q = new PendingPickupQueue();
		q.TryEnqueue(1, 42, Evidence(1f), nowMs: 0);
		q.TryEnqueue(2, 42, Evidence(0.5f), nowMs: 0);

		var first = q.TryTakeFirst(42);
		Assert.True(first is { Sender: 1 }, "the earliest queued claim settles first");

		var losers = q.TakeByItem(42);
		Assert.True(losers.Single().Sender == 2, "the later claim is the explicit loser surface");
	}

	[Fact]
	public void TakeWhere_RemovesOnlyMatchingClaims()
	{
		var q = new PendingPickupQueue();
		q.TryEnqueue(1, 42, Evidence(1f), nowMs: 0);
		q.TryEnqueue(1, 43, Evidence(1f), nowMs: 0);
		q.TryEnqueue(1, 44, Evidence(1f), nowMs: 0);

		var contained = q.TakeWhere(p => p.ItemId is 42 or 43);
		Assert.True(contained.Count == 2);
		Assert.True(q.Count == 1);
		Assert.True(q.TryTakeFirst(44) is not null);
	}

	[Fact]
	public void TakeExpired_IsBoundedByTheHoldWindow()
	{
		var q = new PendingPickupQueue(holdMs: 500);
		q.TryEnqueue(1, 42, Evidence(1f), nowMs: 0);
		q.TryEnqueue(1, 43, Evidence(1f), nowMs: 300);

		Assert.True(q.TakeExpired(499).Count == 0, "499 ms: nothing expired");
		Assert.True(q.TakeExpired(500).Single().ItemId == 42, "500 ms: the first claim expired");
		Assert.True(q.TakeExpired(800).Single().ItemId == 43, "800 ms: the later claim expired on its own schedule");
	}

	[Fact]
	public void Reset_ClearsEveryClaim()
	{
		var q = new PendingPickupQueue();
		q.TryEnqueue(1, 42, Evidence(1f), nowMs: 0);
		q.Reset();
		Assert.True(q.Count == 0);
		Assert.Null(q.TryTakeFirst(42));
	}
}
