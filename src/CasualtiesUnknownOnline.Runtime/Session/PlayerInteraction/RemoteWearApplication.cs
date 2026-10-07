using System.Collections.Generic;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;

namespace CasualtiesUnknownOnline.Runtime.Session.PlayerInteraction;

/// <summary>
/// Pure application of a cross-player wearable placement to a character
/// snapshot. It validates the two native wearable rules that are facts about the
/// AFFECTED body's own state — the wear limb exists and is not dismembered, and no
/// other item already occupies the same wear slot — and produces the worn wire
/// item with the character snapshot's negative slot encoding
/// (<c>-(limbIndex + 2)</c>). WHERE the item goes is the item's own data, answered
/// by <see cref="IWearSemantics"/> rather than by a CUO id table. No game assembly,
/// no state, no I/O — the same path is used by the host authority and L0 tests.
/// </summary>
internal static class RemoteWearApplication
{
	/// <summary>
	/// Try to place <paramref name="source"/> as a wearable on the target
	/// snapshot: <paramref name="limbIndex"/> is the placement the game's own item
	/// data resolves to, <paramref name="wearSlotId"/> the slot the game's
	/// occupancy check compares. Returns false when the target limb is
	/// missing/dismembered or the target already occupies the same wear slot.
	/// </summary>
	internal static bool TryCreateWornItem(
		IWearSemantics semantics,
		int limbIndex,
		string wearSlotId,
		IReadOnlyList<CharacterLimbMsg> limbs,
		IReadOnlyList<CharacterItemMsg> items,
		CharacterItemMsg source,
		out CharacterItemMsg wornItem)
	{
		wornItem = null!;
		if (limbIndex < 0
			|| limbIndex >= limbs.Count
			|| limbs[limbIndex].Dismembered)
		{
			return false;
		}

		if (HasSlotConflict(semantics, items, source.InstanceId, wearSlotId))
		{
			return false;
		}

		wornItem = PlayerCharacterAccess.CloneItem(source);
		wornItem.SlotIndex = -(limbIndex + 2);
		return true;
	}

	/// <summary>
	/// The native occupancy rule: a worn item whose own data carries the SAME wear
	/// slot id (<c>Body.GetWearableBySlotID</c>, the check <c>Body.WearWearable</c>
	/// opens with). The slot id identifies the collision rather than the limb — two
	/// wearables in one slot are in each other's way even when they name different
	/// limbs — and it is asked of the candidate's own data, so content the deleted
	/// table never carried takes part in it too.
	/// <para>
	/// The comparison rides the seam's placement answer, so an occupant whose own
	/// data resolves nothing counts as free. Native compares the raw ids and would
	/// refuse; the divergence needs a worn item whose <c>desiredWearLimb</c> does
	/// not resolve on the host's own limb array, which is the same single-prefab
	/// assumption the seam's limb half documents. Reported rather than hidden, and
	/// the alternative — a second seam member returning the slot alone — buys
	/// nothing for a state the session cannot produce.
	/// </para>
	/// </summary>
	private static bool HasSlotConflict(
		IWearSemantics semantics,
		IReadOnlyList<CharacterItemMsg> items,
		ulong sourceInstanceId,
		string wearSlotId)
	{
		foreach (var item in items)
		{
			if (item.SlotIndex >= 0 || item.InstanceId == sourceInstanceId)
			{
				continue;
			}

			if (semantics.TryGetWearPlacement(item.ItemId, out _, out var wornSlot)
				&& wornSlot == wearSlotId)
			{
				return true;
			}
		}

		return false;
	}
}
