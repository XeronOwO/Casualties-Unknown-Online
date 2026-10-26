namespace CasualtiesUnknownOnline.GameAdapter.Items;

/// <summary>
/// The one object a cross-player eat's delegate CREATED to replace the item it was handed — the peel's opened fruit
/// (<c>Item.cs:2619</c>: <c>body.PickUpItem(Instantiate(Resources.Load("stonefruitopen"), …))</c>).
///
/// <para>
/// The object is the OWNER's row becoming another definition in place: it must get no instance id, no spawn and no pickup
/// report (its definition travels back in the eat's outcome instead), and the eat destroys the object once it has read it.
/// Two hooks see it — the instantiation (<c>ItemWorldSync.OnItemInstantiated</c>) and the delegate's own
/// <c>Body.PickUpItem</c> (<c>PickupSync.OnPickedUp</c>) — so the observation is published on the scope rather than read
/// back from a slot: the replacement's slot is wherever the game's own pickup put it, which is not necessarily the slot the
/// eat arranged, and reading a slot missed it (measured 2026-10-26: the eat reported no replacement and the owner's row was
/// destroyed instead of becoming the opened fruit).
/// </para>
///
/// <para>
/// Both hooks ask <see cref="CallContext.Origin.ItemReplacementCreation"/>, so a creation outside the eat's window can never
/// reach this holder; it is cleared when the eat opens its window and consumed when it reads the result.
/// </para>
/// </summary>
internal static class ItemReplacementObservation
{
	private static Item? _created;

	/// <summary>Opens a fresh window: one eat, at most one replacement.</summary>
	internal static void Clear() => _created = null;

	/// <summary>The object the delegate created, when this client saw one — otherwise <c>null</c>.</summary>
	internal static Item? Created => _created;

	/// <summary>Records the object a hook saw created or picked up inside the eat's window (first one wins).</summary>
	internal static void Record(Item item)
	{
		if (_created == null) // Unity object — ==
		{
			_created = item;
		}
	}
}
