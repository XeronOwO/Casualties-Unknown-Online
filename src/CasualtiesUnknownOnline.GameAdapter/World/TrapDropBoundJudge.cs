using System.Collections.Generic;
using CasualtiesUnknownOnline.Runtime.Protocol;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using CasualtiesUnknownOnline.Runtime.Session.Items;
using CasualtiesUnknownOnline.Runtime.Session.World;
using Microsoft.Extensions.Logging;
using UnityEngine;

namespace CasualtiesUnknownOnline.GameAdapter.World;

/// <summary>
/// The host's bound on the drops a member's DESTRUCTIVE entity event carries
/// (<see cref="EntityEventMsg.Drops"/>). A destructive trap or entity death
/// rolls its items on the side that observed the death, and that side is the
/// reporting member — so the report's drop list is a claim, and the only anchor
/// this host can compare it against is the same one S2b uses for a block break:
/// the damaged or destroyed entities' own prefab tables.
/// <para>
/// The rows come from the host's OWN application of the event, never from the
/// report: <see cref="TrapBuildingHealthScope"/> reads the table of every entity
/// this host's blast damaged while the entity still existed, and
/// <see cref="EntityEventSync"/> adds the trap entity's own table, read before
/// the apply. A drop no row can account for is refused BY NAME and ANSWERED
/// (<see cref="ItemRejectMsg.Reason.TrapDropUnaccounted"/>), so the reporter
/// destroys the local copy and both sides end up with the set this host could
/// really account for.
/// </para>
/// <para>
/// The rule is deliberately one-sided, exactly as <see cref="DropBoundPolicy"/>
/// states it: a MISSING drop is the reporter's own loss and is never refused,
/// while an ADDED one is the creation claim this exists to stop. The bound does
/// not decide WHICH entity dropped what — the message carries no per-entity
/// identity — it decides whether the event's own tables can account for the set
/// at all.
/// </para>
/// </summary>
internal sealed class TrapDropBoundJudge(IItemControl items, ILogger<TrapDropBoundJudge> log)
{
	private readonly IItemControl _items = items;
	private readonly ILogger<TrapDropBoundJudge> _log = log;

	/// <summary>
	/// Host: judge the drops a member's event report carries. The input rows are
	/// this host's own capture (its blast's damaged entities plus the trap entity
	/// itself), never a value the reporter supplied. Returns the drops the report
	/// may carry into the kernel batch and the relay; every refused drop has
	/// already been answered.
	/// <para>
	/// A kind OUTSIDE the building-death carrier's profile (<see cref="TrapDamageProfiles"/>)
	/// cannot carry a drop at all: such a report's drops are an item-domain fact, not
	/// the roll of a dying building, so every drop is refused and the kind is named
	/// in the line.
	/// </para>
	/// </summary>
	internal IReadOnlyList<TrapDropEntryMsg>? Bound(
		ulong sender,
		EntityEventKind kind,
		Vector2 position,
		IReadOnlyList<DropBound> bounds,
		IReadOnlyList<TrapDropEntryMsg>? drops)
	{
		if (drops is not { Count: > 0 })
		{
			return drops;
		}

		if (!TrapDamageProfiles.IsDestructive(kind))
		{
			foreach (var drop in drops)
			{
				_items.SendItemReject(sender, drop.ItemId, ItemRejectMsg.Reason.TrapDropUnaccounted);
			}

			_log.LogWarning(
				"[TrapDrop] {Sender}'s {Kind} at ({X:F1},{Y:F1}) claimed {Count} drop(s), but that kind is outside the building-death carrier's profile — refused by name ({Refused}); nothing materialized, nothing relayed.",
				sender, kind, position.x, position.y, drops.Count, Describe(drops));
			return [];
		}

		var outcome = DropBoundPolicy.JudgeBuildingDrops(bounds, drops);
		if (!outcome.AnyRefused)
		{
			return drops;
		}

		foreach (var refused in outcome.Refused)
		{
			_items.SendItemReject(sender, refused.ItemId, ItemRejectMsg.Reason.TrapDropUnaccounted);
		}

		if (bounds.Count == 0)
		{
			_log.LogWarning(
				"[TrapDrop] {Sender}'s {Kind} at ({X:F1},{Y:F1}) claimed {RefusedCount} drop(s), and this host's own application of the event destroyed or damaged no entity with a drop table — all refused by name ({Refused}); a report cannot introduce an entity's drops where this host accounted for none.",
				sender, kind, position.x, position.y, outcome.Refused.Count, Describe(outcome.Refused));
			return outcome.Accepted;
		}

		_log.LogWarning(
			"[TrapDrop] {Sender}'s {Kind} at ({X:F1},{Y:F1}) claimed {RefusedCount} drop(s) the {RowCount} row(s) of the entities this host's own application destroyed or damaged cannot produce — refused by name ({Refused}), {AcceptedCount} drop(s) accepted and relayed.",
			sender, kind, position.x, position.y, outcome.Refused.Count, bounds.Count, Describe(outcome.Refused), outcome.Accepted.Count);
		return outcome.Accepted;
	}

	private static string Describe(IReadOnlyList<TrapDropEntryMsg> drops)
	{
		var parts = new List<string>(drops.Count);
		foreach (var drop in drops)
		{
			parts.Add(drop.Item is { } item ? $"{item.ItemId}@{item.Condition:F2}" : "a drop with no item state");
		}

		return string.Join(", ", parts);
	}
}
