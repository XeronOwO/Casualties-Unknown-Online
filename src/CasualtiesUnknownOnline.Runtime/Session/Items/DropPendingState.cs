namespace CasualtiesUnknownOnline.Runtime.Session.Items;

/// <summary>
/// The drop-operation pending machine (PURE — no Unity, no time source): the
/// game's one drop spans several hooks (DropItem → ThrowItem / re-pick /
/// container load / frame-end flush), the machine carries the operation across
/// them. All decisions are explicit inputs: the matching item id, the current
/// frame (a same-frame flush waits for the throw velocity), whether the
/// pending item is still alive and whether it is a standalone world item. The
/// GameAdapter's ItemDropState holds the game-side mapping (Item/position) and
/// feeds these inputs — this machine is what the tests lock.
/// </summary>
internal sealed class DropPendingState
{
	/// <summary>Frame the drop happened — the report waits one frame so the game's
	/// DropItem → ThrowItem sequence (one player input) has set the final velocity.
	/// The op id links the pending state to its operation trace.</summary>
	private (ulong ItemId, int Frame, long Op)? _pending;

	internal bool HasPending => _pending is not null;

	internal bool IsPendingFor(ulong itemId) => _pending is { } pending && pending.ItemId == itemId;

	/// <summary>Idle → Dropped. Overwrites any prior pending (the caller flushes a
	/// different item first — two drops in one frame, rare).</summary>
	internal void EnterDrop(ulong itemId, int frame, long op) => _pending = (itemId, frame, op);

	/// <summary>Dropped → Idle: the drop resolved WITHOUT a report (re-picked into an
	/// inventory, loaded into a container, destroyed — another path reports the
	/// item's move, or it never happened). Returns the op id for the trace.</summary>
	internal bool TryCancel(ulong itemId, out long op)
	{
		if (!IsPendingFor(itemId))
		{
			op = 0;
			return false;
		}

		op = _pending!.Value.Op;
		_pending = null;
		return true;
	}

	/// <summary>Dropped → Idle: consumed by ThrowItem — report with the op id.</summary>
	internal bool TryConsumeByThrow(ulong itemId, out (ulong ItemId, long Op) dropped)
	{
		if (!IsPendingFor(itemId))
		{
			dropped = default;
			return false;
		}

		var pending = _pending!.Value;
		_pending = null;
		dropped = (pending.ItemId, pending.Op);
		return true;
	}

	/// <summary>
	/// Dropped → Idle (frame-end flush) or stays Dropped. A same-frame flush
	/// (the throw velocity may still land), a destroyed item or an item still
	/// attached to the body (a drag-to-hand re-picked it within the drop frame —
	/// clearing here made that sequence swallow the pending drop forever, "the
	/// dropped flashlight never reported") keeps the pending state; those
	/// resolve through a later hook or the trace's begin-without-end assert.
	/// </summary>
	internal bool TryFlush(int currentFrame, bool alive, bool standalone, out long op)
	{
		if (_pending is not { } pending || currentFrame <= pending.Frame || !alive || !standalone)
		{
			op = 0;
			return false;
		}

		op = pending.Op;
		_pending = null;
		return true;
	}

	/// <summary>Dropped → Idle: the world was left (scene switch / session end) — the
	/// pending item is gone with it. Returns the op id so the trace stays balanced.</summary>
	internal bool TryReset(out long op)
	{
		if (_pending is not { } pending)
		{
			op = 0;
			return false;
		}

		op = pending.Op;
		_pending = null;
		return true;
	}
}
