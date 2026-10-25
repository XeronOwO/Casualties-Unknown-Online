using System.Collections.Generic;
using CasualtiesUnknownOnline.GameAdapter.Items;
using CasualtiesUnknownOnline.GameState.Domains.Items;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;

namespace CasualtiesUnknownOnline.GameAdapter.Character;

/// <summary>
/// Which snapshot entry a clone render should show in one of its owner's slots — the SELECTION rule of
/// <see cref="CloneInventoryRenderer"/>, kept in its own type so the renderer keeps rendering.
///
/// <para>
/// The AUTHORITY chooses: the kernel's row carries the slot itself (`ItemData.SlotIndex`, "the owner-local slot"), so
/// <see cref="ItemCustodyComparison.TryFindRowInSlot"/> names the instance id and the snapshot entry with that id supplies the visual
/// payload the render needs (definition, condition, contents). The snapshot's own slot field is the FALLBACK, and it exists for the
/// frames where the kernel has not judged a row yet — a restore's first snapshot, an item the owner just picked up and has not reported.
/// Without the fallback a clone would blink out for those frames, which is a visual regression rather than a fix.
/// </para>
/// </summary>
internal static class CloneItemSelection
{
	internal static CharacterItemMsg? ItemInSlot(
		IReadOnlyDictionary<ulong, ItemState> rows,
		CharacterDataMsg data,
		ulong ownerSteamId,
		int slotIndex)
	{
		if (rows.Count > 0 && ItemCustodyComparison.TryFindRowInSlot(rows, ownerSteamId, slotIndex, out var instanceId))
		{
			foreach (var entry in data.Items)
			{
				if (entry.InstanceId == instanceId)
				{
					return entry;
				}
			}
		}

		foreach (var entry in data.Items)
		{
			if (entry.SlotIndex == slotIndex)
			{
				return entry;
			}
		}

		return null;
	}
}
