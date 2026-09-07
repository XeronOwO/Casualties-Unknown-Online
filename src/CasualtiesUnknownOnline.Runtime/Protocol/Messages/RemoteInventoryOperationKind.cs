namespace CasualtiesUnknownOnline.Runtime.Protocol.Messages;

/// <summary>
/// The host-authoritative remote-backpack operations a local viewer can request
/// against another player's carried inventory. Ordinary drag-out take still
/// travels through the existing cross-player inventory-take request path; the
/// dedicated Tab-switch remote-backpack transfer is represented here as
/// <see cref="TransferToRequester"/> so it can use the remote-backpack
/// conscious-owner exception.
/// </summary>
public enum RemoteInventoryOperationKind
{
	/// <summary>Drop one remote player's carried item into the world at the owner's position.</summary>
	Drop = 1,

	/// <summary>Move one remote player's carried item into a container owned by that same player.</summary>
	MoveToContainer = 2,

	/// <summary>Empty the liquid stacks of one remote player's water container.</summary>
	Pour = 3,

	/// <summary>Combine two items carried by the same remote player, using the native combine semantics.</summary>
	Combine = 4,

	/// <summary>Use one remote player's carried usable item on that same owner.</summary>
	Use = 5,

	/// <summary>Wear one remote player's carried wearable on that same owner.</summary>
	Wear = 6,

	/// <summary>Load a carried battery into a carried battery-powered item owned by the same remote player.</summary>
	BatteryLoad = 7,

	/// <summary>Unload the battery from a carried battery-powered item owned by the same remote player.</summary>
	BatteryUnload = 8,

	/// <summary>Toggle the favourite flag on one of the remote player's carried items.</summary>
	FavoriteToggle = 9,

	/// <summary>Move one remote player's carried item to a specific inventory slot of that same owner.</summary>
	MoveToSlot = 10,

	/// <summary>Use one remote player's carried item on the requesting player's own body (the held-remote-item medical flow after closing the remote backpack).</summary>
	UseOnSelf = 11,

	/// <summary>Transfer one remote player's carried item to the requesting player's own inventory (the double-Tab remote-backpack transfer path).</summary>
	TransferToRequester = 12,
}
