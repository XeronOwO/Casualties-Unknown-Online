using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using CasualtiesUnknownOnline.Runtime.Session.CharacterData;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Session;

/// <summary>
/// The local body's pending character restore and its cancel rule. The origin is the whole
/// point: a restore a run THIS client owns queued (its CUO continue, its respawn) must not
/// survive into a run this client follows, while one a PEER handed over must — the host
/// sends a reconnecting player its character BEFORE the instruction that starts its follow,
/// so a follow-start cancel that swallowed the peer's restore would throw away the
/// reconnect, including the position applied on the body's first frame. A run this client
/// starts ON ITS OWN is the opposite case: nothing waiting here can belong to it.
/// </summary>
public class LocalCharacterRestoreQueueTests
{
	[Fact]
	public void Queue_MarksTheRestorePending()
	{
		var queue = new LocalCharacterRestoreQueue();
		var queued = Character(100);

		queue.Queue(queued, ownRun: true);

		Assert.True(queue.HasPending);
		Assert.Same(queued, queue.Pending);
		Assert.False(queue.WipePending);
	}

	[Fact]
	public void MarkWipePending_ShowsTheSecondPassIsDue()
	{
		var queue = new LocalCharacterRestoreQueue();
		queue.Queue(Character(100), ownRun: true);

		queue.MarkWipePending();

		Assert.True(queue.WipePending);
		Assert.True(queue.HasPending); // the snapshot stays until the second pass consumes it
	}

	[Fact]
	public void OwnRunQueue_IsDroppedWhenThisClientFollowsAnotherRun()
	{
		var queue = new LocalCharacterRestoreQueue();
		queue.Queue(Character(100), ownRun: true);

		Assert.True(queue.CancelOwnRun());
		Assert.False(queue.HasPending);
		Assert.Null(queue.Pending);
		Assert.False(queue.WipePending);
	}

	[Fact]
	public void PeerQueue_SurvivesThisClientFollowingItsRun()
	{
		// The regression: the follow-start cancel used to swallow the peer's hand-over, which
		// is precisely the restore that follow exists to apply.
		var queue = new LocalCharacterRestoreQueue();
		var handed = Character(200);
		queue.Queue(handed, ownRun: false);

		Assert.False(queue.CancelOwnRun());
		Assert.True(queue.HasPending);
		Assert.Same(handed, queue.Pending);
	}

	[Fact]
	public void PeerQueue_KeepsItsPhaseWhenThisClientFollowsItsRun()
	{
		// The follow cancel only decides whose snapshot it is. A peer entry's apply phase
		// belongs to the body that took the first pass, and that body leaves through its own
		// path — a follow start is not it.
		var queue = new LocalCharacterRestoreQueue();
		queue.Queue(Character(200), ownRun: false);
		queue.MarkWipePending();

		Assert.False(queue.CancelOwnRun());
		Assert.True(queue.HasPending);
		Assert.True(queue.WipePending);
	}

	[Fact]
	public void CancelAll_DropsAPeerHandOverThisClientNeverFollowed()
	{
		// A run this client starts ON ITS OWN (its start click, its own continue) is not the
		// run a peer's hand-over belongs to, so nothing waiting here can survive it.
		var queue = new LocalCharacterRestoreQueue();
		queue.Queue(Character(200), ownRun: false);
		queue.MarkWipePending();

		Assert.True(queue.CancelAll());
		Assert.False(queue.HasPending);
		Assert.False(queue.WipePending);
	}

	[Fact]
	public void Cancel_WithNothingQueued_ReportsNothingDropped()
	{
		var queue = new LocalCharacterRestoreQueue();

		Assert.False(queue.CancelOwnRun());
		Assert.False(queue.CancelAll());
	}

	[Fact]
	public void Queue_ReplacesWhateverWasWaiting_AndRestartsTheApplyPhase()
	{
		// A re-sent restore (the handshake and the InWorld edge both send one) replaces the
		// waiting snapshot. The apply phase belongs to the SNAPSHOT being applied, so the
		// newest one gets the full wipe/stats/items sequence again instead of inheriting a
		// wipe that ran for the older one.
		var queue = new LocalCharacterRestoreQueue();
		queue.Queue(Character(100), ownRun: true);
		queue.MarkWipePending();

		var resent = Character(200);
		queue.Queue(resent, ownRun: true);

		Assert.Same(resent, queue.Pending);
		Assert.False(queue.WipePending);

		// The last queue wins the origin too — what the cancel rule reads is the snapshot
		// that is actually waiting.
		Assert.True(queue.CancelOwnRun());
		Assert.False(queue.HasPending);
	}

	[Fact]
	public void Clear_EndsTheQueue()
	{
		var queue = new LocalCharacterRestoreQueue();
		queue.Queue(Character(100), ownRun: true);
		queue.MarkWipePending();

		queue.Clear();

		Assert.False(queue.HasPending);
		Assert.False(queue.WipePending);
		Assert.False(queue.CancelOwnRun());
		Assert.False(queue.CancelAll());
	}

	private static CharacterDataMsg Character(ulong instanceId) =>
		new() { SlotCount = 5, Items = [new CharacterItemMsg { InstanceId = instanceId, ItemId = "bag", Condition = 1f }] };
}
