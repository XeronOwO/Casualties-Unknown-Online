using System.Collections.Generic;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;

namespace CasualtiesUnknownOnline.Runtime.Session.World;

/// <summary>
/// The host's bound on what a member's destruction report may carry as drops.
/// <para>
/// The roll itself is NOT reproducible on the host, and that is the evidence
/// this stage turned on: the game rolls its block drops with
/// <c>UnityEngine.Random</c> inside <c>WorldGeneration.DamageBlock</c>
/// (<c>reversing/Assembly-CSharp/Assembly-CSharp/WorldGeneration.cs:751-837</c>)
/// — a process-global, UNSEEDED stream that everything else in that process
/// advances (sky colour, footsteps, world generation, earthquakes). The host
/// cannot know where the breaker's stream stood, so which item, how many and
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
/// The rule is deliberately one-sided: a MISSING drop is the breaker's own loss
/// and is never refused, while an ADDED one is the injection this exists to
/// stop. A report claiming fewer drops than the table allows therefore passes,
/// and that is the intended failure direction.
/// </para>
/// </summary>
internal static class BlockDropBoundPolicy
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
	internal readonly record struct Outcome(IReadOnlyList<BlockDropEntryMsg> Accepted, IReadOnlyList<BlockDropEntryMsg> Refused)
	{
		internal bool AnyRefused => Refused.Count > 0;
	}

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
	/// it, and a report never loses more drops than it must.
	/// </para>
	/// </summary>
	internal static Outcome Judge(IReadOnlyList<BlockDropBound> bounds, IReadOnlyList<BlockDropEntryMsg>? drops)
	{
		if (drops is not { Count: > 0 })
		{
			return new Outcome([], []);
		}

		var rowOwner = new int[bounds.Count];
		for (var row = 0; row < rowOwner.Length; row++)
		{
			rowOwner[row] = -1; // 0 is a drop index, so "no owner" needs its own value
		}
		var accepted = new List<BlockDropEntryMsg>(drops.Count);
		var refused = new List<BlockDropEntryMsg>();
		for (var drop = 0; drop < drops.Count; drop++)
		{
			if (Assign(bounds, drops, drop, new bool[bounds.Count], rowOwner))
			{
				accepted.Add(drops[drop]);
				continue;
			}

			refused.Add(drops[drop]);
		}

		return new Outcome(accepted, refused);
	}

	/// <summary>
	/// Kuhn's augmenting path: give this drop a row, moving a drop that already
	/// holds one to ANOTHER row of its own when that is what makes room. Bounded by
	/// the report's size (a break's drop set is a handful of rows either way), and
	/// <paramref name="seen"/> is per root drop, which is what keeps the walk from
	/// revisiting a row it already tried.
	/// </summary>
	private static bool Assign(IReadOnlyList<BlockDropBound> bounds, IReadOnlyList<BlockDropEntryMsg> drops, int drop, bool[] seen, int[] rowOwner)
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

	private static bool CanAccountFor(BlockDropBound bound, BlockDropEntryMsg drop) =>
		drop.Item is { } item
		&& !string.IsNullOrEmpty(item.ItemId)
		&& bound.AccountsFor(item.ItemId, item.Condition, ConditionSlack);
}
