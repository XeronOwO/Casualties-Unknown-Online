using CasualtiesUnknownOnline.GameAdapter.Character;
using CasualtiesUnknownOnline.GameAdapter.Items;
using Microsoft.Extensions.Logging;

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
	internal static bool Apply(Body body, ulong itemInstanceId, GameAdapterDomains domains)
	{
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

		using var scope = CallContext.Enter(CallContext.Origin.CharacterItemUse);
		body.UseItem(item);
		domains.Log.LogInformation("[ItemUse] ran the game's own eat of {Type} (id {ItemId}) on the local body.", item.id, itemInstanceId);
		return true;
	}
}
