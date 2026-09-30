using System.Collections.Generic;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using CasualtiesUnknownOnline.Runtime.Session.Items;

namespace CasualtiesUnknownOnline.Runtime.Session.CharacterData;

/// <summary>
/// The reconnect restore's merge half: the host's transfer table — its
/// authoritative per-id record of what the guest owns — over the guest's last
/// character snapshot. It is a type of its own because the two records state
/// different things, and the rules that combine them are the whole of it.
///
/// <para>
/// The table is a per-id record, and for a container that is the limit of it: an
/// entry captured as a pickup DIGEST states its contents as bare instance ids, an
/// entry the kernel rebuilt states none at all, and one a full carried-sync built
/// carries them whole — which is why an entry can sometimes be appended below.
/// Placement in general is the SNAPSHOT's: it is the recursive capture the restore
/// consumes, where a container's contents ride inside the parent
/// (<see cref="CharacterItemMsg.Contents"/>), and the same rule already held for
/// the slot (a carried item's slot is its owner's local fact). The entry
/// contributes its STATE.
/// </para>
///
/// <para>
/// The match is RECURSIVE for that reason. A container's content is a snapshot
/// node of the same shape, so an entry stating one must update that node where it
/// is: indexing only the top level made a contained id look absent, appended it
/// beside its container, and the restore then dropped it (its slot is its
/// parent's, so there is nothing to restore it into) while the container itself
/// came back empty — the reconnect lost exactly the contents it was handed (batch
/// 20260930-g).
/// </para>
///
/// <para>
/// An entry the snapshot does not carry ANYWHERE is placed from what the entry
/// itself states: a body slot (>= 0) or a limb (the wear encoding, &lt;= -2).
/// -1 is the container-content/world/unknown value (<c>ItemStateCodec.SlotOf</c>),
/// and a container content's own entry always states it — the parent is the
/// snapshot's fact, and this record cannot state one (the entry's ParentItemId is
/// 0 where the table builds it, <c>ItemArbitration</c>). Such an entry is COUNTED
/// as unplaced and named by the caller instead of being appended: an appended
/// content goes to the restore's wearable path, which drops it while blaming the
/// limb encoding. The reachable case is an item moved into a container after the
/// guest's last 1 Hz report; it is a declared loss, not a silent one.
/// </para>
/// </summary>
internal static class TransferTableRestoreMerge
{
	/// <summary>How the merge went: entries that updated a node the snapshot already carried, entries appended because the snapshot did not carry the id anywhere and the entry states a body placement, and entries the snapshot cannot place at all — counted, never guessed.</summary>
	internal readonly record struct Outcome(int Matched, int Appended, int Unplaced);

	/// <summary>Merge every entry into <paramref name="data"/> in place — the snapshot the reconnecting guest will receive.</summary>
	internal static Outcome Apply(CharacterDataMsg data, IReadOnlyList<WorldItem> transferred)
	{
		var byId = new Dictionary<ulong, CharacterItemMsg>();
		var byDef = new Dictionary<string, CharacterItemMsg>();
		IndexSnapshot(data.Items, byId, byDef);

		var matched = 0;
		var appended = 0;
		var unplaced = 0;
		foreach (var entry in transferred)
		{
			var authoritative = entry.Item;
			if (FindNode(authoritative, byId, byDef) is { } node)
			{
				TakeState(node, authoritative);
				matched++;
				continue;
			}

			// The snapshot states no node for this id, so the entry's own placement is the
			// only one left. -1 means "inside a container, in the world, or unknown", and
			// appending such a node would send the restore looking for a limb that is not
			// there — the loss is counted for the caller to name instead.
			if (authoritative.SlotIndex == -1)
			{
				unplaced++;
				continue;
			}

			data.Items.Add(authoritative);
			appended++;
		}

		return new Outcome(matched, appended, unplaced);
	}

	/// <summary>Index every node of the restore's shape — the top level and every container's contents — because those are the nodes an entry may state. A node with no instance id is indexed by its definition id; an EMPTY definition id indexes nothing, deliberately: an empty key would let a definition-less entry match it, and a spurious match states the wrong item.</summary>
	private static void IndexSnapshot(List<CharacterItemMsg> items, Dictionary<ulong, CharacterItemMsg> byId, Dictionary<string, CharacterItemMsg> byDef)
	{
		foreach (var item in items)
		{
			if (item.InstanceId != 0)
			{
				byId[item.InstanceId] = item;
			}
			else if (item.ItemId is { Length: > 0 })
			{
				byDef[item.ItemId] = item;
			}

			IndexSnapshot(item.Contents, byId, byDef);
		}
	}

	/// <summary>The node an entry states — anywhere in the shape — or null when the snapshot does not carry the id anywhere. An id-less entry falls back to its definition id, as the flat top-level lookup always did.</summary>
	private static CharacterItemMsg? FindNode(CharacterItemMsg authoritative, Dictionary<ulong, CharacterItemMsg> byId, Dictionary<string, CharacterItemMsg> byDef)
	{
		if (authoritative.InstanceId != 0 && byId.TryGetValue(authoritative.InstanceId, out var byKey))
		{
			return byKey;
		}

		return authoritative.ItemId is { Length: > 0 } && byDef.TryGetValue(authoritative.ItemId, out var byDefinition)
			? byDefinition
			: null;
	}

	/// <summary>
	/// Take the entry's authoritative state onto the node the snapshot carries, and leave
	/// the node's placement alone: its slot (the owner's local fact) and its contents (the
	/// container's structure — the two things an entry cannot be trusted to state, since a
	/// pickup digest and an entry the kernel rebuilt state neither).
	/// </summary>
	private static void TakeState(CharacterItemMsg node, CharacterItemMsg authoritative)
	{
		node.Condition = authoritative.Condition;
		node.Favourited = authoritative.Favourited;
		node.Liquids = authoritative.Liquids;
		node.Components = authoritative.Components;
	}
}
