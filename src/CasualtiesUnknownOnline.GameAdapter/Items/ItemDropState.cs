using UnityEngine;

namespace CasualtiesUnknownOnline.GameAdapter.Items;

/// <summary>
/// The drop-operation state: one player drop spans several hooks (DropItem →
/// ThrowItem / re-pick / container load / frame-end flush) and the pending
/// state carries the operation across them. Named phases replace the
/// free-floating pending tuple in ItemWorldSync — every change is a
/// transition, the failure paths are explicit (destroyed while pending, world
/// left → Reset) instead of lingering until the next drop overwrites them, and
/// all pending read/write points live in ONE class (state belongs to its
/// owner). The migration DECISIONS (what to report, when to trace) stay with
/// ItemWorldSync — this class only answers "which transition happened".
/// </summary>
internal sealed class ItemDropState
{
	/// <summary>The lifecycle of one drop report.</summary>
	internal enum Phase
	{
		/// <summary>No drop in flight.</summary>
		Idle,

		/// <summary>A drop happened but the report waits — for the final throw velocity (ThrowItem lands a moment later) or the frame-end flush; a re-pick / container load / destruction cancels it.</summary>
		Dropped,
	}

	/// <summary>Frame the drop happened — the report waits one frame so the game's DropItem → ThrowItem sequence (one player input) has set the final velocity. The op id links the pending state to its operation trace.</summary>
	private (Item Item, Vector2 Pos, int Frame, long Op)? _pending;

	internal Phase Current => _pending is null ? Phase.Idle : Phase.Dropped;

	/// <summary>True when the pending drop belongs to this item (Unity ==).</summary>
	internal bool IsPendingFor(Item item) => _pending is { } pending && pending.Item == item;

	/// <summary>Idle → Dropped. The caller flushes any prior pending drop of a DIFFERENT item first (two drops in one frame — rare).</summary>
	internal void EnterDrop(Item item, Vector2 pos, long op) =>
		_pending = (item, pos, Time.frameCount, op);

	/// <summary>Dropped → Idle: the drop resolved WITHOUT a report (re-picked into an inventory, loaded into a container, destroyed — another path reports the item's move, or it never happened). Returns the op id for the trace.</summary>
	internal bool TryCancel(Item item, out long op)
	{
		if (!IsPendingFor(item))
		{
			op = 0;
			return false;
		}

		op = _pending!.Value.Op;
		_pending = null;
		return true;
	}

	/// <summary>Dropped → Idle: consumed by ThrowItem — report with the drop position and the op id.</summary>
	internal bool TryConsumeByThrow(Item item, out (Item Item, Vector2 Pos, long Op) dropped)
	{
		if (!IsPendingFor(item))
		{
			dropped = default;
			return false;
		}

		var pending = _pending!.Value;
		_pending = null;
		dropped = (pending.Item, pending.Pos, pending.Op);
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
	internal bool TryFlush(out (Item Item, Vector2 Pos, long Op) dropped)
	{
		if (_pending is not { } pending)
		{
			dropped = default;
			return false;
		}

		if (Time.frameCount <= pending.Frame
			|| pending.Item == null // Unity object — ==; destroyed while pending
			|| !ItemWorldSync.IsStandaloneWorldItem(pending.Item))
		{
			dropped = default;
			return false;
		}

		_pending = null;
		dropped = (pending.Item, pending.Pos, pending.Op);
		return true;
	}

	/// <summary>Dropped → Idle: the world was left (scene switch / session end) — the pending item is gone with it. Returns the op id so the trace stays balanced.</summary>
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
