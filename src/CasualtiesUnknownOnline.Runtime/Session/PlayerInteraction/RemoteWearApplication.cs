using System.Collections.Generic;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;

namespace CasualtiesUnknownOnline.Runtime.Session.PlayerInteraction;

/// <summary>
/// Pure application of a cross-player wearable placement to a character
/// snapshot. It validates the native wearable rules that the Runtime can see
/// without the game assembly (target limb exists and is not dismembered, no
/// other item already occupies the same wear slot) and produces the worn wire
/// item with the character snapshot's negative slot encoding
/// (<c>-(limbIndex + 2)</c>). No game assembly, no state, no I/O — the same
/// path is used by the host authority and L0 tests.
/// </summary>
public static class RemoteWearApplication
{
	/// <summary>
	/// Try to place <paramref name="source"/> as a wearable on the target
	/// snapshot. Returns false when the item is not a known wearable, the
	/// target limb is missing/dismembered, or the target already occupies the
	/// same wear slot.
	/// </summary>
	public static bool TryCreateWornItem(
		IReadOnlyList<CharacterLimbMsg> limbs,
		IReadOnlyList<CharacterItemMsg> items,
		CharacterItemMsg source,
		out CharacterItemMsg wornItem)
	{
		wornItem = null!;
		if (!RemoteWearCatalog.TryGet(source.ItemId, out var profile))
		{
			return false;
		}

		if (profile.LimbIndex < 0
			|| profile.LimbIndex >= limbs.Count
			|| limbs[profile.LimbIndex].Dismembered)
		{
			return false;
		}

		if (HasSlotConflict(items, source.InstanceId, profile.WearSlotId))
		{
			return false;
		}

		wornItem = PlayerCharacterAccess.CloneItem(source);
		wornItem.SlotIndex = -(profile.LimbIndex + 2);
		return true;
	}

	private static bool HasSlotConflict(
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

			if (RemoteWearCatalog.TryGet(item.ItemId, out var worn)
				&& worn.WearSlotId == wearSlotId)
			{
				return true;
			}
		}

		return false;
	}
}
