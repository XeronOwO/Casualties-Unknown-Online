using System;
using System.Collections.Generic;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;

namespace CasualtiesUnknownOnline.GameAdapter.Items;

/// <summary>
/// One owner's carried rows as the set of standing item objects they ask for, flattened: which ids must
/// exist locally, which data row each one carries, and which row is its parent in the DATA (never in the
/// scene — the materializer keeps the tree flat, ticket
/// <c>mod-cross-player-solid-food-semantics</c> §6 constraint 5).
///
/// <para>
/// It is a pure reading of the data, deliberately separate from the scene work: the carried fact table
/// hands over a recursive <c>Contents</c> tree (the 1 Hz snapshot, the carried-event merge, the
/// starting-supply merge), and every rule about which of those rows becomes an object — no id yet, the
/// data holds the id as a world row, a duplicate id deeper in the tree — is decided here, where it can be
/// pinned without a Unity scene. The materializer owns what happens to a scene object; this owns what the
/// data says should exist. Its truth table is pinned by <c>StandingItemPlanTests</c>.
/// </para>
///
/// <para>
/// The ORDER is the contract the materializer relies on: <see cref="Ids"/> lists a parent before its
/// contents, so an object's data parent is always a known row by the time its child is created.
/// </para>
/// </summary>
internal sealed class StandingItemPlan
{
	private readonly List<ulong> _ids = [];
	private readonly Dictionary<ulong, ulong> _parents = [];
	private readonly Dictionary<ulong, CharacterItemMsg> _rows = [];

	private StandingItemPlan(ulong owner)
	{
		Owner = owner;
	}

	/// <summary>The member whose carried rows these are.</summary>
	internal ulong Owner { get; }

	/// <summary>The rows in parent-before-child order.</summary>
	internal IReadOnlyList<ulong> Ids => _ids;

	/// <summary>How many rows the data asks to have incarnated.</summary>
	internal int Count => _ids.Count;

	/// <summary>
	/// Read one owner's carried tree. <paramref name="isWorldRow"/> is the data arm of the category
	/// (<c>IItemControl.IsWorldItemRegistered</c>): a row whose id the authoritative data holds as a WORLD
	/// item is not a carried row any more — the world path owns that object — and it is skipped here rather
	/// than incarnated a second time.
	/// </summary>
	internal static StandingItemPlan Build(ulong owner, IReadOnlyList<CharacterItemMsg> items, Func<ulong, bool> isWorldRow)
	{
		var plan = new StandingItemPlan(owner);
		plan.Walk(items, 0, isWorldRow);
		return plan;
	}

	/// <summary>Whether the data asks for this id.</summary>
	internal bool Contains(ulong instanceId) => _rows.ContainsKey(instanceId);

	/// <summary>The row the data carries for an id.</summary>
	internal CharacterItemMsg Row(ulong instanceId) => _rows[instanceId];

	/// <summary>The id's parent row in the data, 0 for a top-level row (a slot or a limb home).</summary>
	internal ulong ParentOf(ulong instanceId) => _parents[instanceId];

	/// <summary>
	/// Depth-first, parents before children, with two rows that never become objects:
	///
	/// <para>
	/// A row with NO instance id — the starting-supply window before the ids are bound, where there is
	/// nothing to address it by. Its CONTENTS are still walked, and a bound content keeps the nearest
	/// id-bearing row above it as its data parent: a container's id binding and its child's are separate
	/// facts, and one arriving before the other must not cost the child its object.
	/// </para>
	///
	/// <para>
	/// A row the DATA holds as a world item — the category's other arm, and not a carried row any more.
	/// That row takes its whole subtree with it: the world path materializes a world container's contents
	/// itself (it restores them as real children), so planning them here would put two objects on one id.
	/// </para>
	///
	/// <para>
	/// A duplicate id keeps its FIRST position: the tree the game captures has one row per item, and a
	/// repeat would only ever be a corrupt capture, where recursing twice is the one answer that could not
	/// terminate.
	/// </para>
	/// </summary>
	private void Walk(IReadOnlyList<CharacterItemMsg> items, ulong parentId, Func<ulong, bool> isWorldRow)
	{
		foreach (var item in items)
		{
			if (item.InstanceId == 0)
			{
				Walk(item.Contents, parentId, isWorldRow);
				continue;
			}

			if (isWorldRow(item.InstanceId))
			{
				continue;
			}

			if (_rows.ContainsKey(item.InstanceId))
			{
				continue;
			}

			_rows[item.InstanceId] = item;
			_parents[item.InstanceId] = parentId;
			_ids.Add(item.InstanceId);
			Walk(item.Contents, item.InstanceId, isWorldRow);
		}
	}
}
