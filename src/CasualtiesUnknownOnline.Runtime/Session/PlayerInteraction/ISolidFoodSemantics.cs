namespace CasualtiesUnknownOnline.Runtime.Session.PlayerInteraction;

/// <summary>
/// The game's own content facts the cross-player SOLID-FOOD chain judges with,
/// instead of the deleted <c>RemoteConsumeCatalog</c> id/number table. The
/// Runtime holds the QUESTION and every call site; the Game Adapter answers it
/// from the game's own registry (<c>Item.GlobalItems</c>), because no game
/// assembly may cross into the Runtime.
/// <para>
/// The question is the item's own <c>useAction</c> — the delegate
/// <c>Body.UseItem</c> runs (<c>Body.cs:2475-2481</c>) — and the answer is its
/// shape: does it feed the eating body, and what does it do to the item object.
/// The game carries no field for either: <c>ItemInfo</c> has no nutrition member
/// and its <c>category</c> string is a display classifier the unidentified-item
/// label reads (<c>PlayerCamera.ItemHoverDescription</c>), not an edibility
/// one — the vanilla edibles the deleted table never carried are filed under
/// <c>"custom"</c> (<c>geofruit</c>, <c>browncap</c>, <c>popfruit</c>, …). The
/// amounts and the body writes are literals inside the delegate, so the
/// delegate's own code is the only authority that can answer it; that is why the
/// adapter reads the compiled body rather than a data field
/// (<see cref="SolidFoodVerdict"/> names what it looks for).
/// </para>
/// <para>
/// A mod item qualifies exactly like a vanilla one: it is judged by its own
/// registered delegate, so no table has to be extended for it.
/// </para>
/// </summary>
public interface ISolidFoodSemantics
{
	/// <summary>
	/// The shape of this item's own use action, or
	/// <see cref="SolidFoodVerdict.NotSolidFood"/> for an id the game does not
	/// know, an item the game may not use, and an item whose use action does not
	/// feed a body.
	/// </summary>
	SolidFoodVerdict Classify(string itemId);
}
