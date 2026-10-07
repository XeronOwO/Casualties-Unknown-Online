namespace CasualtiesUnknownOnline.GameAdapter.Items;

/// <summary>
/// The STANDING ITEM OBJECT category's classifier: the local, non-authoritative incarnation of an
/// item the authoritative data carries as a member's CARRIED row, present on a client that does not
/// hold the item itself (ticket <c>mod-cross-player-solid-food-semantics</c>).
///
/// <para>
/// WHY IT IS A CATEGORY OF ITS OWN. Every existing item family is classified by its transform chain
/// — <see cref="ItemWorldSync.IsWorldItem"/> walks up looking for <c>InventorySlot</c>,
/// <c>Body</c> or <c>Limb</c>, and <see cref="ItemWorldSync.IsStandaloneWorldItem"/> adds "and no
/// <c>Container</c> above it". A standing object is parented to a CUO-owned holder, so that chain
/// reads it as a WORLD item: the full-table reconcile would kill it on the first keyframe, the host
/// would stream its parked position as a world fact, the guest's follow pump would switch it to
/// local physics, and a remote pickup/destroy would address it. The data disagrees — a carried row
/// is not a world row — and this class is where the code reads the data instead of the parent chain.
/// </para>
///
/// <para>
/// THE DECISION IS THE DATA'S. <see cref="Is"/> asks the item data by instance id (the one query
/// <c>IItemControl</c> hands over for this purpose), never a scene state: the data answers for an id
/// whatever the scene currently holds, it is the same fact the reconcile, the position stream and
/// the kernel already read, and the category therefore cannot drift from the data it mirrors. The
/// <see cref="StandingItemObject"/> marker carries the other half — what the object was CREATED as.
/// Both arms are load-bearing: without the marker every parentless id-bearing object would classify
/// as standing (a local drop whose report the host has not accepted yet is exactly that shape, and
/// the world table only learns it from the next keyframe), and without the data arm an object would
/// stay standing after the data moved its id into the world.
/// </para>
///
/// <para>
/// CONSULTED WHERE. The two shared classifiers, <see cref="ItemWorldSync.IsWorldItem"/> and
/// <see cref="ItemWorldSync.IsStandaloneWorldItem"/>, ask this class, so every one of their call
/// sites — the census in the ticket's §3, from the reconcile's kill loop through the position
/// stream, the position follow, the item application and the container classifier — answers for a
/// standing object by construction. The sites whose verdict is NOT "is this a world item" ask it
/// directly: the use report (<c>ItemUseSync.OnItemUsed</c>, whose routing the food chain owns), the
/// destroy report (<c>ItemWorldSync.OnItemDestroyed</c>) and the pickup gate
/// (<c>DoPickupCheckPatch</c>, the one native gate every local gesture on an item goes through).
/// </para>
/// </summary>
internal static class StandingItems
{
	/// <summary>
	/// True for a standing item object: the materializer stamped it
	/// (<see cref="StandingItemObject"/>) AND the authoritative data does not hold its id as a world
	/// row. An id-less standing object and a client with no bound item data both answer true — the
	/// marker decides what the data cannot contradict.
	/// </summary>
	internal static bool Is(Item item) =>
		item.GetComponent<StandingItemObject>() != null // Unity object — ==
		&& !IsWorldRow(item);

	/// <summary>
	/// The data arm, asked by id: an id the item data holds as a world row is a world item whatever
	/// the scene holds. No id, or no bound item data, answers false — nothing says the id is in the
	/// world.
	/// </summary>
	private static bool IsWorldRow(Item item)
	{
		var idComp = item.GetComponent<ItemInstanceId>();
		return idComp != null && idComp.Id != 0 && PatchBridge.ItemCategory?.IsWorldItemRegistered(idComp.Id) == true;
	}
}
