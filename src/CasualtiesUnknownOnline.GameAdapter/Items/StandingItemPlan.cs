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

	/// <summary>
	/// The same plan, with the SET taken from the authority: the kernel's rows decide which ids this owner has, and the snapshot
	/// supplies each one's payload (a standing object needs the definition and contents to instantiate). Rows are ordered
	/// parent-before-child by their own `ParentItemId`, which is the order the materializer relies on.
	///
	/// <para>
	/// A row with no snapshot entry is skipped: the object cannot be built without its payload, and the fact table's next fire carries it.
	/// When the kernel holds NO row for the owner — a restore's first frames, a session whose rows have not been replayed yet — the plan
	/// falls back to the snapshot walk above, which is what keeps the objects that already exist from blinking out while the authority
	/// catches up.
	/// </para>
	/// </summary>
	internal static StandingItemPlan BuildFromRows(
		ulong owner,
		IReadOnlyList<CharacterItemMsg> items,
		Func<ulong, bool> isWorldRow,
		IReadOnlyDictionary<ulong, GameState.Domains.Items.ItemState> rows)
	{
		if (rows.Count == 0)
		{
			return Build(owner, items, isWorldRow);
		}

		var owned = new Dictionary<ulong, ulong>();
		foreach (var row in rows)
		{
			if (row.Value.Location.Owner.Value == owner && !isWorldRow(row.Key))
			{
				owned[row.Key] = row.Value.Location.ParentItemId;
			}
		}

		if (owned.Count == 0)
		{
			return Build(owner, items, isWorldRow);
		}

		var plan = new StandingItemPlan(owner);
		foreach (var id in ParentsFirst(owned))
		{
			if (!plan.TryFindRow(items, id, out var payload))
			{
				continue;
			}

			var parent = owned[id];
			plan._rows[id] = payload;
			plan._parents[id] = owned.ContainsKey(parent) ? parent : 0;
			plan._ids.Add(id);
		}

		return plan;
	}

	/// <summary>The owned ids ordered so a parent always precedes its contents; a parent outside the set is a top-level row.</summary>
	private static IReadOnlyList<ulong> ParentsFirst(IReadOnlyDictionary<ulong, ulong> owned)
	{
		var ordered = new List<ulong>(owned.Count);
		var placed = new HashSet<ulong>();
		foreach (var id in owned.Keys)
		{
			var chain = new List<ulong>();
			var cursor = id;
			while (owned.ContainsKey(cursor) && placed.Add(cursor))
			{
				chain.Add(cursor);
				var parent = owned[cursor];
				if (parent == 0 || !owned.ContainsKey(parent))
				{
					break;
				}

				cursor = parent;
			}

			for (var i = chain.Count - 1; i >= 0; i--)
			{
				if (!ordered.Contains(chain[i]))
				{
					ordered.Add(chain[i]);
				}
			}
		}

		return ordered;
	}

	/// <summary>The snapshot's own row for an id, at any depth of the carried tree.</summary>
	private bool TryFindRow(IReadOnlyList<CharacterItemMsg> items, ulong instanceId, out CharacterItemMsg row)
	{
		foreach (var item in items)
		{
			if (item.InstanceId == instanceId)
			{
				row = item;
				return true;
			}

			if (TryFindRow(item.Contents, instanceId, out row))
			{
				return true;
			}
		}

		row = null!;
		return false;
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
