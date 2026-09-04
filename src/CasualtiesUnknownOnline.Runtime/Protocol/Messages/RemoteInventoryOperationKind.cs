namespace CasualtiesUnknownOnline.Runtime.Protocol.Messages;

/// <summary>
/// The host-authoritative remote-backpack operations a local viewer can request
/// against another player's carried inventory. Take/transfer-to-local is not
/// part of this enum: it already travels through the existing cross-player
/// inventory-take request path.
/// </summary>
public enum RemoteInventoryOperationKind
{
	/// <summary>Drop one remote player's carried item into the world at the owner's position.</summary>
	Drop = 1,

	/// <summary>Move one remote player's carried item into a container owned by that same player.</summary>
	MoveToContainer = 2,

	/// <summary>Empty the liquid stacks of one remote player's water container.</summary>
	Pour = 3,
}
