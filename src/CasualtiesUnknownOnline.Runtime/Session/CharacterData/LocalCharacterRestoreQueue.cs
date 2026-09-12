using CasualtiesUnknownOnline.Runtime.Protocol.Messages;

namespace CasualtiesUnknownOnline.Runtime.Session.CharacterData;

/// <summary>
/// The local body's pending character restore: WHICH snapshot is waiting for a body,
/// which run queued it, and whether its first pass already wiped the body's slots.
///
/// The origin decides the cancel rule, and getting it wrong loses a player's character
/// in one direction and strands an abandoned run's in the other:
///
/// - a restore queued by a run THIS client owns (its own CUO continue, its own
///   next-level respawn) is dropped when a run starts that this client follows instead
///   — that run will never reach a body;
/// - a restore handed over by a PEER is NOT dropped there. The host sends a reconnecting
///   player its character before the instruction that starts its follow, so that
///   snapshot belongs to exactly the follow being started — swallowing it would throw
///   away the reconnect, including the position applied on the body's first frame,
///   before the spawn is reported back.
/// - a run this client starts ON ITS OWN (its start click, its own continue) drops
///   whatever is queued, a peer hand-over included: a hand-over this client never
///   followed belongs to a run that is not this one.
///
/// The two-frame apply is a PHASE of this queue, not of the caller: the first pass
/// wipes the body's slots (the game destroys them at the end of the frame) and only the
/// second pass can put the restored items back. The phase therefore belongs to the BODY
/// that took the first pass — the owner of that body clears the queue when it leaves, so
/// a later body can never receive half a restore.
///
/// Pure state, no Unity types: the Game Adapter owns the body and the position gate; this
/// owns the pending restore and its cancel rule.
/// </summary>
public sealed class LocalCharacterRestoreQueue
{
	private CharacterDataMsg? _pending;
	private bool _ownRun;
	private bool _wipePending;

	/// <summary>True = a restore is waiting for the local body (the 1 Hz report is suppressed while one is).</summary>
	public bool HasPending => _pending is not null;

	/// <summary>The waiting snapshot; null when nothing is queued.</summary>
	public CharacterDataMsg? Pending => _pending;

	/// <summary>True = the first pass ran (the body's slots were wiped) and only the items are left to put back.</summary>
	public bool WipePending => _wipePending;

	/// <summary>
	/// Queue a restore. <paramref name="ownRun"/> is the origin the cancel rule reads: true
	/// for a run this client owns (its CUO continue, its respawn), false for a snapshot a
	/// peer handed over (the host's reconnect restore). A re-sent restore replaces the
	/// waiting one AND restarts the apply phase — the phase belongs to the snapshot being
	/// applied, so the newest one gets the full wipe/stats/items sequence.
	/// </summary>
	public void Queue(CharacterDataMsg data, bool ownRun)
	{
		_pending = data;
		_ownRun = ownRun;
		_wipePending = false;
	}

	/// <summary>
	/// A run this client STARTS ON ITS OWN is taking over: nothing that waited here can
	/// belong to it, so everything goes — including a peer's hand-over this client never
	/// followed. Returns whether anything was dropped; the caller logs that, because a
	/// silent cancel is what would make a lost restore look like a working one.
	/// </summary>
	public bool CancelAll()
	{
		if (_pending is null)
		{
			return false;
		}

		Clear();
		return true;
	}

	/// <summary>
	/// This client is FOLLOWING a run someone else announced: the restore its OWN run
	/// queued can never reach a body now, while a peer's hand-over is exactly the restore
	/// this follow is for and stays. Returns whether anything was dropped.
	///
	/// The first pass's <see cref="WipePending"/> goes with an own-run entry, because its
	/// body left with the run. A PEER entry's phase is not this method's business: it is
	/// cleared where the body that took it leaves the world.
	/// </summary>
	public bool CancelOwnRun()
	{
		if (_pending is null || !_ownRun)
		{
			return false;
		}

		Clear();
		return true;
	}

	/// <summary>The first pass ran (stats applied, the body's slots wiped): the queue now waits for the second one.</summary>
	public void MarkWipePending() => _wipePending = true;

	/// <summary>The restore reached the body, its body left, or the session ended — nothing is pending any more.</summary>
	public void Clear()
	{
		_pending = null;
		_ownRun = false;
		_wipePending = false;
	}
}
