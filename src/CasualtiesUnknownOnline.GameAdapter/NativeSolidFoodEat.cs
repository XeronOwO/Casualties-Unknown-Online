using CasualtiesUnknownOnline.GameAdapter.Character;
using CasualtiesUnknownOnline.GameAdapter.Items;
using Microsoft.Extensions.Logging;
using UnityEngine;

namespace CasualtiesUnknownOnline.GameAdapter;

/// <summary>
/// Runs the cross-player SOLID-FOOD eat on the AFFECTED side's client. The
/// solid-food family is the migration's end point: a food's <c>useAction</c> is
/// a delegate that writes the eating body AND the item it is handed, so there is
/// nothing the host could compute and nothing the operator could measure — the
/// eat is the game's own <c>Body.UseItem</c> → <c>Stats.useAction</c>
/// (<c>Body.cs:2475-2481</c>) run against the eater's own body and the standing
/// object the host addressed, which is the eater's own local incarnation of the
/// offered item (ticket <c>mod-cross-player-solid-food-semantics</c>, step 3).
/// <para>
/// EVERY native branch therefore happens on the body it lands on: the hunger
/// clamp at 125 and its overflow into sickness, the vomit and burp rolls (plain
/// <c>Random</c>) and the dirty-hands roll inside <c>Body.Eat</c>, the per-item
/// body-field writes, the talker reactions, the item's own condition cost, the
/// sprite a bitten fruit shows and — for a food that swaps its container — the
/// game's own decision about it. The resulting item state then goes back through
/// the report → host arbitration → result path, so the item, whose ownership
/// never moved, is updated on its owner's own item.
/// </para>
/// <para>
/// The call runs inside the item-use sound scope, exactly as a local eat's clip
/// does, so the crunch and the item's other clips are relayed to the peers at
/// the eater's position instead of being swallowed; it runs OUTSIDE the
/// <c>RemoteApply</c> scope for the sibling families' reason — that scope is the
/// "this is a replay of a peer's fact" marker, and this is a real effect on this
/// body, so its clips must be relayed just as a local eat's are.
/// </para>
/// </summary>
internal static class NativeSolidFoodEat
{
	/// <summary>
	/// Run the eat for one item id on this client; returns whether the game's own
	/// use action ran. Every refusal is logged by name, because a silent no-op
	/// after a host-admitted use is the one outcome nobody could diagnose.
	/// </summary>
	internal static bool Apply(Body body, ulong itemInstanceId, GameAdapterDomains domains) =>
		Apply(body, itemInstanceId, domains, out _);

	/// <summary>
	/// The same call, with the delegate's own REPLACEMENT observation handed back: the definition and condition of the
	/// object the action left in the item's place, when it replaced the item rather than only consuming it. The owner's
	/// client is the only side that can show it (the item is the owner's), so the caller reports it as the outcome.
	/// </summary>
	internal static bool Apply(Body body, ulong itemInstanceId, GameAdapterDomains domains, out SolidFoodReplacement replacement)
	{
		replacement = SolidFoodReplacement.None;

		if (itemInstanceId == 0)
		{
			return false;
		}

		// THE EATER'S OWN ITEM FIRST. When the gift already made the item the eater's carried item, the
		// game's own delegate finds it in a slot and every step works.
		//
		// "EAT FROM MY HAND" IS THE OTHER CASE, AND IT IS AN EXTENSION RATHER THAN A DEVIATION (user's
		// ruling, 2026-10-25): the item stays the giver's, and the target consumes it out of that hand —
		// the transfer-first shape was rejected outright, because the target may have no free slot at all.
		// The item is then this client's standing object for the giver's item, and the delegate's own slot
		// steps (`SlotOf`/`DropItem`) resolve against the EATER's body, which does not hold it. That is the
		// defect this ticket exists for: the closed fruit was consumed for its owner with no peel, and the
		// pancake's single bite read as the whole item. The hand shape needs the delegate's slot steps
		// pointed at the OWNER's body, which is the next step of the run sheet and NOT something a silent
		// stand-in can fake.
		var carriedByEater = CarriedItemLocator.TryFind(body, itemInstanceId, out var item);
		if (!carriedByEater && !domains.StandingMaterializer.TryGetStandingItem(itemInstanceId, out item))
		{
			domains.Log.LogWarning("[ItemUse] the eat of item {ItemId} cannot run: neither this body's carried tree nor the standing objects hold that id on this client.", itemInstanceId);
			return false;
		}

		// The category, not only the object: the data may have moved this id into the
		// world (its owner dropped it) while the scene still holds it under the body,
		// and eating an item that just left the inventory would feed this body for a
		// meal nobody offers. A standing object is the GIVER's carried row, so this
		// guard asks the same question of it through the standing classifier.
		if (carriedByEater ? ItemWorldSync.IsWorldItem(item) : !StandingItems.Is(item))
		{
			domains.Log.LogWarning("[ItemUse] the eat of item {ItemId} cannot run: its data row is not a carried row on this client.", itemInstanceId);
			return false;
		}

		if (!item.Stats.usable)
		{
			// The game's own gate (`Body.UseItem` tests it too), refused here so the
			// line says which item and why instead of a silent no-op.
			domains.Log.LogWarning("[ItemUse] the eat of {Type} (id {ItemId}) cannot run: the game's own data says the item is not usable.", item.id, itemInstanceId);
			return false;
		}

		// THE HAND CASE'S SLOT STEPS. The delegates resolve their own slot work against the body they are
		// handed — `Body.SlotOf` searches `body.slots`, and `Body.GetItem` reads a slot's first child — so an
		// item that sits nowhere in the eater's slots answers "slot 0" and the peel's replacement lands by
		// luck, while `DropItem(item)` no-ops because the item is not carried. The arrangement below makes
		// the game's own reads answer truthfully for the duration of the call: the standing item is parented
		// into an EMPTY hand slot (the game's own slot shape, `slots[i].transform`), and taken back out
		// afterwards. It is not a transfer: the scope suppresses every report the arrangement could cause,
		// and the custody tables are never told anything (the user's ruling — "eat from my hand" — and its
		// reason, that a full inventory must still be feedable).
		var arrangedSlot = -1;
		var originalParent = item.transform.parent;
		using var scope = CallContext.Enter(CallContext.Origin.CharacterItemUse);
		using var handFeed = carriedByEater ? null : CallContext.Enter(CallContext.Origin.HandFeed);
		if (!carriedByEater)
		{
			arrangedSlot = FirstEmptyHandSlot(body);
			if (arrangedSlot >= 0)
			{
				item.transform.SetParent(body.slots[arrangedSlot].transform, worldPositionStays: true);
			}
		}

		var replacementObject = (Item?)null;
		try
		{
			// THE DELEGATE'S OWN CREATION IS A REPLACEMENT, NOT A NEW ITEM: a food that swaps its object (the peel instantiates the opened
			// fruit and picks it up) created a phantom — a brand-new instance id reported as a world spawn at the origin, while the owner's
			// row became the replacement in place (measured 2026-10-26, `[ItemSpawn] materializing stonefruitopen … at (0.0,0.0)`). The scope
			// tells the creation report that what the delegate makes is this item's replacement, so no id is allocated and no spawn goes out;
			// both hooks that see it (instantiation and the delegate's own pickup) publish it to the observation.
			ItemReplacementObservation.Clear();
			using (CallContext.Enter(CallContext.Origin.ItemReplacementCreation))
			{
				body.UseItem(item);
			}

			// Read the replacement from the OBSERVATION first: the delegate's own `PickUpItem` puts the opened fruit into whatever slot the
			// game finds first, which is not necessarily the slot this client arranged, so a slot read missed it (measured the same evening:
			// the eat reported no replacement and the owner's row was destroyed instead). The slot read stays as the fallback for a delegate
			// that leaves its object where the arranged item stood.
			replacementObject = carriedByEater
				? null
				: ItemReplacementObservation.Created ?? ReadReplacement(body, arrangedSlot, item);

			domains.Log.LogInformation(
				"[ItemUse] ran the game's own eat of {Type} (id {ItemId}) on the local body{Hand}.",
				item.id,
				itemInstanceId,
				carriedByEater ? string.Empty : $" out of the giver's hand (arranged into slot {arrangedSlot})");
		}
		finally
		{
			// The delegate may have consumed, dropped or replaced the item; only put back what is still
			// the same live object sitting in the slot we arranged, so a destroyed or re-parented item
			// cannot be yanked out of wherever the game's own step put it.
			if (arrangedSlot >= 0 && item != null // Unity object — ==
				&& item.transform.parent == body.slots[arrangedSlot].transform) // Unity object — ==
			{
				item.transform.SetParent(originalParent, worldPositionStays: true);
			}
		}

		// THE REPLACEMENT THE DELEGATE MADE, observed where it was made. The peel instantiates the opened fruit at
		// the closed one's position and puts it into the SAME slot — and in the hand case that slot is the one this
		// client arranged, so the object is here and is the EATER's only by accident of who ran the delegate. It must
		// not enter the eater's inventory: the item never changed hands (the user's ruling), and the vanilla visible
		// result is "the item in the giver's hand became the peeled one". So the object is destroyed here and its
		// definition travels back, for the OWNER's own item to become it in place.
		if (replacementObject != null) // Unity object — ==
		{
			replacement = new SolidFoodReplacement(replacementObject.id, replacementObject.condition);
			domains.Log.LogInformation(
				"[ItemUse] the eat of {Type} (id {ItemId}) replaced the item with {Replacement} (condition {Condition:F2}); the owner's own item becomes it in place.",
				item.id,
				itemInstanceId,
				replacement.ItemId,
				replacement.Condition);
			Object.Destroy(replacementObject.gameObject);
		}

		return true;
	}

	/// <summary>
	/// The object the delegate left in the arranged slot when the item it was handed is no longer there: the peel's
	/// replacement, or null when the action consumed the item without replacing it. Read BEFORE anything restores the
	/// arrangement, because the restoration is what would hide it.
	/// </summary>
	private static Item? ReadReplacement(Body body, int arrangedSlot, Item original)
	{
		if (arrangedSlot < 0 || arrangedSlot >= body.slots.Length)
		{
			return null;
		}

		var slot = body.slots[arrangedSlot].transform;
		if (slot.childCount == 0)
		{
			return null;
		}

		var child = slot.GetChild(0).GetComponent<Item>();
		return child == null || child == original ? null : child; // Unity objects — ==
	}

	/// <summary>
	/// The eater's first empty hand slot, or <c>-1</c> when both hands are full. Hands are slots 0 and 1 —
	/// the game's own order, and the pair every inventory patch reports as "Hands".
	/// </summary>
	private static int FirstEmptyHandSlot(Body body)
	{
		for (var slot = 0; slot <= 1 && slot < body.slots.Length; slot++)
		{
			if (body.slots[slot].transform.childCount == 0)
			{
				return slot;
			}
		}

		return -1;
	}
}
