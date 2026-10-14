namespace CasualtiesUnknownOnline.Runtime.Session.Commands;

/// <summary>
/// Which property of a targeted item a write addresses. The set is the kernel's
/// own payload (<c>ItemData</c>) rather than a curated subset: the two SavedItem
/// scalars, one liquid stack of the item's container and one typed field of one of
/// its saveable components.
/// </summary>
internal enum ItemDataProperty
{
	/// <summary>The item's condition (durability/charge — the game's own scalar).</summary>
	Condition,

	/// <summary>The item's favourite flag.</summary>
	Favourited,

	/// <summary>One stack of the item's liquid container, by its liquid id.</summary>
	Liquid,

	/// <summary>One field of one of the item's captured components, by component type name and field name.</summary>
	Component,
}
