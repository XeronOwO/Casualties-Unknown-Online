using System;
using System.Collections.Generic;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;

namespace CasualtiesUnknownOnline.Runtime.Session.World;

/// <summary>
/// The host's bound on what a member's destruction report may carry as drops.
/// <para>
/// The roll itself is NOT reproducible on the host, and that is the evidence
/// this stage turned on: the game rolls its drops with <c>UnityEngine.Random</c>
/// — inside <c>WorldGeneration.DamageBlock</c> for a block
/// (<c>reversing/Assembly-CSharp/Assembly-CSharp/WorldGeneration.cs:751-837</c>)
/// and inside <c>BuildingEntity.Update</c> for a dying entity
/// (<c>reversing/Assembly-CSharp/Assembly-CSharp/BuildingEntity.cs:74-120</c>) —
/// a process-global, UNSEEDED stream that everything else in that process
/// advances (sky colour, footsteps, world generation, earthquakes). The host
/// cannot know where the reporter's stream stood, so which item, how many and
/// which condition a legitimate roll produced cannot be re-derived. A check that
/// re-rolled would refuse ordinary play.
/// </para>
/// <para>
/// The BOUND is what can be checked: the report must lie inside the destroyed
/// thing's own table. This machine is that half — given the table's rows and the
/// reported drops, it splits them into the drops a row can account for and the
/// drops no roll could have produced. The host refuses the latter and relays
/// only the former, so an item no local roll can create never enters the world
/// on any side.
/// </para>
/// <para>
/// The rule is deliberately one-sided: a MISSING drop is the reporter's own loss
/// and is never refused, while an ADDED one is the injection this exists to
/// stop. A report claiming fewer drops than the table allows therefore passes,
/// and that is the intended failure direction.
/// </para>
/// <para>
/// TWO message shapes carry the same claim — a block break's own drops
/// (<see cref="BlockDropEntryMsg"/>) and the building-death drops that ride the
/// same report (<see cref="TrapDropEntryMsg"/>) — and they answer to one rule,
/// so both go through the same <see cref="Partition{TDrop}"/> rather than two
/// matchers that could drift apart.
/// </para>
/// </summary>
internal static class DropBoundPolicy
{
	/// <summary>
	/// The float slack a condition window comparison allows. The value crosses one
	/// protobuf float field unchanged and is read off the item at its own
	/// <c>Start</c>, so an exact comparison would in fact be right; the slack is
	/// here because the game's windows are 0.15 wide and a rounding difference
	/// must never be the reason a legitimate roll is refused. It cannot admit a
	/// value the game's own roll could not produce in any meaningful way.
	/// </summary>
	private const float ConditionSlack = 1e-3f;

	/// <summary>One report's drops, split by whether a table row can account for each.</summary>
	internal readonly record struct Outcome<TDrop>(IReadOnlyList<TDrop> Accepted, IReadOnlyList<TDrop> Refused)
	{
		internal bool AnyRefused => Refused.Count > 0;
	}

	/// <summary>The destroyed BLOCK's own table, against the drops a break report carries.</summary>
	internal static Outcome<BlockDropEntryMsg> Judge(IReadOnlyList<DropBound> bounds, IReadOnlyList<BlockDropEntryMsg>? drops) =>
		Partition(bounds, drops, static drop => new DropClaim(drop.Item?.ItemId, drop.Item?.Condition ?? 0f));

	/// <summary>The same judgement over the same rule, for the building-death drops that ride a break report: their table is the dying entity's own prefab data, read while the entity was still alive.</summary>
	internal static Outcome<TrapDropEntryMsg> JudgeBuildingDrops(IReadOnlyList<DropBound> bounds, IReadOnlyList<TrapDropEntryMsg>? drops) =>
		Partition(bounds, drops, static drop => new DropClaim(drop.Item?.ItemId, drop.Item?.Condition ?? 0f));

	/// <summary>
	/// Split <paramref name="drops"/> against <paramref name="bounds"/>, each row
	/// accounting for at most one drop. A drop carrying no item state at all is
	/// refused with the rest: it names nothing a receiver could materialize.
	/// <para>
	/// The rows are matched to the drops as a SET rather than one drop at a time:
	/// assigning greedily in report order would refuse a legitimate drop whose only
	/// row a later drop could equally have claimed — two authored rows over the
	/// same item id with different windows (a custom tile may declare that), read in
	/// the order that puts the wide row first. An augmenting path moves the earlier
	/// drop to another row when that is what makes room, so a drop is refused only
	/// when NO assignment of the whole report to the table's rows can account for
	/// it, and a report never loses more drops than it must. The COUNT of refusals
	/// is what is minimal, not the choice among equals: when several drops could
	/// only have come from the same rows, which one loses out follows report
	/// order, so a refusal names a drop no row can account for without claiming
	/// that THAT one specifically was forged.
	/// </para>
	/// </summary>
	private static Outcome<TDrop> Partition<TDrop>(IReadOnlyList<DropBound> bounds, IReadOnlyList<TDrop>? drops, Func<TDrop, DropClaim> claim)
	{
		if (drops is not { Count: > 0 })
		{
			return new Outcome<TDrop>([], []);
		}

		// The claim is read ONCE per drop: the matcher walks the same drops several
		// times (one augmenting path per root drop), and a projection that re-read
		// the message each time would read state a receiver may have rewritten.
		var claims = new DropClaim[drops.Count];
		for (var drop = 0; drop < claims.Length; drop++)
		{
			claims[drop] = claim(drops[drop]);
		}

		var rowOwner = new int[bounds.Count];
		for (var row = 0; row < rowOwner.Length; row++)
		{
			rowOwner[row] = -1; // 0 is a drop index, so "no owner" needs its own value
		}
		var accepted = new List<TDrop>(drops.Count);
		var refused = new List<TDrop>();
		for (var drop = 0; drop < claims.Length; drop++)
		{
			if (Assign(bounds, claims, drop, new bool[bounds.Count], rowOwner))
			{
				accepted.Add(drops[drop]);
				continue;
			}

			refused.Add(drops[drop]);
		}

		return new Outcome<TDrop>(accepted, refused);
	}

	/// <summary>
	/// Kuhn's augmenting path: give this drop a row, moving a drop that already
	/// holds one to ANOTHER row of its own when that is what makes room. Bounded by
	/// the report's size (a break's drop set is a handful of rows either way), and
	/// <paramref name="seen"/> is per root drop, which is what keeps the walk from
	/// revisiting a row it already tried.
	/// </summary>
	private static bool Assign(IReadOnlyList<DropBound> bounds, IReadOnlyList<DropClaim> drops, int drop, bool[] seen, int[] rowOwner)
	{
		for (var row = 0; row < bounds.Count; row++)
		{
			if (seen[row] || !CanAccountFor(bounds[row], drops[drop]))
			{
				continue;
			}

			seen[row] = true;
			if (rowOwner[row] < 0 || Assign(bounds, drops, rowOwner[row], seen, rowOwner))
			{
				rowOwner[row] = drop;
				return true;
			}
		}

		return false;
	}

	private static bool CanAccountFor(DropBound bound, DropClaim drop) =>
		drop.ItemId is { Length: > 0 } itemId
		&& bound.AccountsFor(itemId, drop.Condition, ConditionSlack);

	/// <summary>What a reported drop claims about itself: the two facts a table row can answer for.</summary>
	private readonly record struct DropClaim(string? ItemId, float Condition);
}
