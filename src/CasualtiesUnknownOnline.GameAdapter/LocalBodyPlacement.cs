using System.Collections.Generic;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using Microsoft.Extensions.Logging;

namespace CasualtiesUnknownOnline.GameAdapter;

/// <summary>
/// Puts an already-materialized item into a slot THIS body accepts, verifying every attempt by
/// reading the slot back: the game's own placement refuses a native <c>onlyHoldInHands</c> item
/// outside a hand slot EVEN with <c>force</c> (Body.cs:1390), so a placement that trusts the call can
/// log a slot the item never entered. The item's own requested slot is a preference; every other
/// free slot is tried in index order.
///
/// <para>
/// Split out of <see cref="PlayerInventoryPlacement"/> because a second caller needs the same rule:
/// the character restore's UNPLACED row — an item the archive carries with slot -1, because the cut
/// landed while the member's body had it and the report that states its slot had not arrived yet.
/// The body's own rules place it; the archive never guesses a slot. Two copies of this loop would
/// eventually disagree about what a body accepts, and the one that guessed would be the one that
/// lost the item.
/// </para>
/// </summary>
internal static class LocalBodyPlacement
{
	/// <summary>Place <paramref name="placed"/> on <paramref name="body"/>, answering the slot it landed in. False = this body accepts none of its free slots; the caller decides what that means for the item it handed in.</summary>
	internal static bool TryPlaceInSlot(
		Body body,
		CharacterItemMsg item,
		Item placed,
		ILogger log,
		string source,
		out int placedSlot) =>
		TryPlaceInBodySlot(body, item, placed, log, source, out placedSlot);

	/// <summary>
	/// The loop itself — private, one level below the entry, because the source-shape gate pins this
	/// exact body: the object the caller materialized is the object handed to the body, and every
	/// attempt is verified by reading the slot back.
	/// </summary>
	private static bool TryPlaceInBodySlot(
		Body body,
		CharacterItemMsg item,
		Item placed,
		ILogger log,
		string source,
		out int placedSlot)
	{
		var requested = item.SlotIndex;
		foreach (var slot in SlotCandidates(body, item))
		{
			body.PickUpItem(placed, slot, force: true);
			if (body.GetItem(slot) != placed) // Unity objects — ==
			{
				continue;
			}

			item.SlotIndex = slot;
			if (requested >= 0 && requested != slot)
			{
				log.LogWarning("[{Source}] destination slot {Requested} is not usable for {ItemId} (id {InstanceId}) — using slot {Slot} instead.",
					source, requested, item.ItemId, item.InstanceId, slot);
			}

			log.LogInformation("[{Source}] placed {ItemId} (id {InstanceId}) in local slot {Slot}.",
				source, item.ItemId, item.InstanceId, slot);
			placedSlot = slot;
			return true;
		}

		placedSlot = -1;
		return false;
	}

	/// <summary>The slots to try, in order: the one the item states when it is free, then every other free slot.</summary>
	private static List<int> SlotCandidates(Body body, CharacterItemMsg item)
	{
		var candidates = new List<int>(body.slots.Length);
		if (item.SlotIndex >= 0 && item.SlotIndex < body.slots.Length && !body.HoldingItem(item.SlotIndex))
		{
			candidates.Add(item.SlotIndex);
		}

		for (var slot = 0; slot < body.slots.Length; slot++)
		{
			if (!body.HoldingItem(slot) && !candidates.Contains(slot))
			{
				candidates.Add(slot);
			}
		}

		return candidates;
	}
}
