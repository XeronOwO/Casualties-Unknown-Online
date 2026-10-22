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
	/// The first slot this body WOULD accept the item in, the requested one first — asked without placing
	/// anything. The give's placement answer runs before the item exists on this client, and the host records
	/// what this returns, so the answer and the placement must be the same rule: <see cref="WouldAccept"/> is
	/// that rule, and the placement loop below builds its candidates from it.
	/// </summary>
	internal static int FirstAcceptingSlot(Body body, Item probe, int requestedSlot)
	{
		if (WouldAccept(body, probe, requestedSlot))
		{
			return requestedSlot;
		}

		for (var slot = 0; slot < body.slots.Length; slot++)
		{
			if (slot != requestedSlot && WouldAccept(body, probe, slot))
			{
				return slot;
			}
		}

		return -1;
	}

	/// <summary>
	/// The game's own pickup gate with <c>force</c> (<c>Body.cs:1390</c>): the slot is free, and an
	/// <c>onlyHoldInHands</c> item may only enter a hand. <c>force</c> bypasses the canPickUp, holding and
	/// distance guards, so those three are what a placement has left to satisfy — and a placement that trusted
	/// the call alone would log a slot the item never entered.
	/// </summary>
	internal static bool WouldAccept(Body body, Item probe, int slot) =>
		slot >= 0
		&& slot < body.slots.Length
		&& !body.HoldingItem(slot)
		&& (!probe.Stats.onlyHoldInHands || body.slots[slot].isHand);

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
		foreach (var slot in SlotCandidates(body, placed, requested))
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

	/// <summary>The slots to try, in order: the one the item states when this body would accept it, then every other acceptable slot.</summary>
	private static List<int> SlotCandidates(Body body, Item placed, int requestedSlot)
	{
		var candidates = new List<int>(body.slots.Length);
		if (requestedSlot >= 0 && WouldAccept(body, placed, requestedSlot))
		{
			candidates.Add(requestedSlot);
		}

		for (var slot = 0; slot < body.slots.Length; slot++)
		{
			if (!candidates.Contains(slot) && WouldAccept(body, placed, slot))
			{
				candidates.Add(slot);
			}
		}

		return candidates;
	}
}
