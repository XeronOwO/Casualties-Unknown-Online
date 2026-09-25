namespace CasualtiesUnknownOnline.Runtime.Session.PlayerInteraction;

/// <summary>One step of the native call the owner replays, in native order.</summary>
internal enum RemoteIntentStep
{
	/// <summary>R9's first step: the held item leaves its slot (<c>Body.DropItem(item)</c>).</summary>
	DropHeldItem = 1,

	/// <summary>R9's occupying-slot step: the item holding the destination slot leaves it (<c>Body.DropItem(slot)</c>).</summary>
	DropSlotItem = 2,

	/// <summary><c>Body.PickUpItem(item, slot, false)</c> — the game's own guards decide.</summary>
	PickUp = 3,
}
