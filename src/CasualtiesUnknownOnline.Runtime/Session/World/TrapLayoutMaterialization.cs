using System.Collections.Generic;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;

namespace CasualtiesUnknownOnline.Runtime.Session.World;

/// <summary>
/// The trap-layout materialization dedup judgment (PURE — no Unity): which
/// entries of an alignment's to-materialize list describe the SAME entity and
/// must therefore instantiate ONE copy.
/// <para>
/// One entity can produce several layout entries — a turret is both the
/// TurretFired and the TurretSelfDestructed position, a mine is both
/// MinePressed and MineExploded (<c>TrapEntityScan</c>) — and all of them carry
/// the same prefab name at the same position. Materializing per entry would
/// instantiate a duplicate copy; since the entries of a runtime-created entity
/// also carry the SAME creation key, the two copies would share one identity
/// and the runtime-entity snapshot could bind either. The identity of an
/// entity on this wire is (prefab name, position): the entry that carries the
/// creation key wins over a keyless sibling, because only that entry can stamp
/// the copy.
/// </para>
/// </summary>
internal static class TrapLayoutMaterialization
{
	/// <summary>Collapse entries describing the same entity; the keyed entry wins over a keyless sibling, otherwise the first wins. Input order is otherwise preserved.</summary>
	internal static TrapLayoutMaterializationResult Deduplicate(IReadOnlyList<TrapLayoutEntryMsg> entries)
	{
		var byEntity = new Dictionary<(string PrefabName, float X, float Y), int>();
		var result = new List<TrapLayoutEntryMsg>(entries.Count);
		var collapsed = 0;
		foreach (var entry in entries)
		{
			var key = (entry.PrefabName, entry.X, entry.Y);
			if (!byEntity.TryGetValue(key, out var index))
			{
				byEntity[key] = result.Count;
				result.Add(entry);
				continue;
			}

			collapsed++;
			if (result[index].CreationKey is null && entry.CreationKey is not null)
			{
				result[index] = entry; // the keyed sibling replaces the keyless one in place
			}
		}

		return new TrapLayoutMaterializationResult { Entries = result, CollapsedCount = collapsed };
	}
}
