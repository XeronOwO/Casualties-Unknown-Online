using System;
using CasualtiesUnknownOnline.Runtime.Protocol;
using CasualtiesUnknownOnline.Runtime.Session.Items;
using Microsoft.Extensions.Logging;
using UnityEngine;

namespace CasualtiesUnknownOnline.GameAdapter.Items;

/// <summary>
/// The report commit funnel for the item sync paths: every network report goes
/// through ONE commit — Rejected → no message, otherwise send (the Func
/// returns the message count) and close the operation with the disposition
/// (Committed, or Indeterminate when the game state was too ambiguous to
/// verify — the report still goes out, marked as evidence for the next bug
/// hunt). "Report only after a verified commit" is a code path, not a comment
/// (AGENTS.md #9 — a postfix once reported a swallowed write that never
/// happened). The drop report itself (SendDropReport) lives here too.
/// </summary>
internal sealed class ItemReportCommitter(
	ItemService items,
	OperationTrace trace,
	ILogger<ItemReportCommitter> log)
{
	private readonly ItemService _items = items;
	private readonly OperationTrace _trace = trace;
	private readonly ILogger<ItemReportCommitter> _log = log;

	/// <summary>How a report commit resolved: the write landed (Committed), the game state could not be reliably verified (Indeterminate), or the write clearly did not happen — no message (Rejected).</summary>
	internal enum CommitStatus { Committed, Rejected, Indeterminate }

	/// <summary>The unified report commit: Rejected → no message; otherwise send (the Func returns the message count for the trace) and close the operation with the disposition.</summary>
	internal int CommitReport(ulong itemId, long op, string origin, CommitStatus status, Func<int> send, params string[] events)
	{
		if (status == CommitStatus.Rejected)
		{
			_trace.End(op, itemId, origin, "Rejected", events);
			return 0;
		}

		var msgs = send();
		_trace.End(op, itemId, origin, status == CommitStatus.Indeterminate ? "Indeterminate" : $"Committed({msgs})", events);
		return msgs;
	}

	internal void SendDropReport(ulong itemId, Item item, Vector2 pos)
	{
		// Diagnostic: how many contents rode along (a dropped bag must carry
		// its contents — "the bag is empty after dropping" class of bugs).
		var container = item.GetComponent<Container>();
		_log.LogInformation("[ItemDropped] {Type} (id {ItemId}) at ({X:F1},{Y:F1}), vel ({VX:F1},{VY:F1}) — container contents {Contents}.",
			item.id, itemId, pos.x, pos.y, item.rb.velocity.x, item.rb.velocity.y,
			container != null ? container.transform.childCount : 0); // Unity object — ==
		_items.SendItemDropped(itemId, ItemStateCodec.CaptureItem(item, -1),
			new NetVector2(pos.x, pos.y),
			new NetVector2(item.rb.velocity.x, item.rb.velocity.y),
			0, item.transform.eulerAngles.z, default, item.rb.angularVelocity);
	}
}
