namespace CasualtiesUnknownOnline.Abstractions;

/// <summary>
/// Where one item is, as <see cref="IModItemState"/> reports it. The five
/// members name the kernel's own location families one by one — an item is in
/// exactly one of them at a time — and they are deliberately NOT an encoding a
/// caller may arithmetic on.
///
/// <para>
/// An item's IDENTITY does not change with this value: the instance id survives
/// every move between these families, including onto a limb and back off it.
/// <see cref="Applied"/> and <see cref="Terminal"/> are the two that are not
/// inventory positions, and they are the two an inventory projection cannot
/// show.
/// </para>
/// </summary>
[ApiStability(ApiStabilityLevel.Experimental)]
public enum ModItemLocation
{
	/// <summary>Loose in the world; <see cref="IModItemState.X"/> and <see cref="IModItemState.Y"/> carry its position.</summary>
	World,

	/// <summary>
	/// Held by a player — in their inventory, or worn on a limb (a worn item is
	/// carried with a negative slot index). The slot itself is in that player's
	/// <see cref="IModPlayerInventory"/>, which is the projection that lists
	/// what a player carries.
	/// </summary>
	Carried,

	/// <summary>Inside another item; <see cref="IModItemState.ParentItemId"/> names the container.</summary>
	Contained,

	/// <summary>
	/// The item IS the installed form of a limb component: a splint or a
	/// tourniquet that the game's own limb action turned into a component on
	/// somebody's limb. It is held by the limb rather than carried, so no
	/// inventory gesture, search or frisk reaches it, and that player's
	/// <see cref="IModPlayerInventory"/> deliberately does not list it. The same
	/// id reads <see cref="Carried"/> again once the component is taken off.
	/// </summary>
	Applied,

	/// <summary>
	/// The item's life ended — consumed, destroyed, or replaced as the source of
	/// a cook or a craft. The id is never issued again, so this value is final,
	/// and the row stays readable on purpose: "this id ended" is a different
	/// answer from "this id never existed".
	/// </summary>
	Terminal,
}
