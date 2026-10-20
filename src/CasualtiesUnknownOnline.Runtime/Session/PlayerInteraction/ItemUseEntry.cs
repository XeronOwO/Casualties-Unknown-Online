namespace CasualtiesUnknownOnline.Runtime.Session.PlayerInteraction;

/// <summary>
/// The entry a cross-player item gesture was made through. The item's own data can
/// legitimately put one item in two families — a drinkable bag holding a
/// health-usable liquid is both the inventory use's drink and the wound view's
/// topical application — and only the gesture says which action the player asked
/// for, so every routing site asks the family WITH this input (decision 246: "a
/// world drag is the inventory use, a drag onto a limb of the wound view is the
/// limb use, and an item that can do both is two actions nothing may arbitrate
/// between").
/// </summary>
public enum ItemUseEntry
{
	/// <summary>
	/// The item's own use action — native <c>Body.UseItem</c>
	/// (<c>Body.cs:2475-2481</c>) for a drink or a meal, beside
	/// <c>Body.WearWearable</c> on the same radial-centre branch
	/// (<c>PlayerCamera.cs:1636-1651</c>) for a wearable — which is what the world
	/// drag is the cross-player form of. It carries no limb, so no limb rule may be
	/// asked of it.
	/// </summary>
	Inventory,

	/// <summary>
	/// The item's own limb action — native <c>PlayerCamera.ApplyWoundItem</c>'s
	/// <c>useLimbAction</c> / <c>WaterContainerItem.ApplyToLimb</c>
	/// (<c>PlayerCamera.cs:739-763</c>), which is what a release onto a wound-view
	/// limb is. It names the limb, and that is the one thing the inventory use cannot.
	/// </summary>
	Limb,
}
