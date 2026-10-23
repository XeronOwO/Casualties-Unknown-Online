namespace CasualtiesUnknownOnline.GameAdapter;

/// <summary>
/// Which context a dragged item is in (user ruling 2026-10-23). Inside the player's OWN UI the drag means the
/// local body and nothing else: no other player's detection range is read, no backpack is opened and no
/// cross-player release exists. Only in the world does a player's detection range open their backpack, and only a
/// DISPLAYED backpack offers the give (a slot) or the use/wear (its centre).
/// </summary>
internal enum GiveGestureMode
{
	/// <summary>The player's own ring or one of their own panels is open: local semantics only.</summary>
	OwnUi,

	/// <summary>No own UI and no displayed backpack: the detection range selects whose backpack opens.</summary>
	World,

	/// <summary>Another player's backpack is displayed: its slots give, its centre uses/wears, its range keeps switching the display.</summary>
	DisplayedBackpack,
}
