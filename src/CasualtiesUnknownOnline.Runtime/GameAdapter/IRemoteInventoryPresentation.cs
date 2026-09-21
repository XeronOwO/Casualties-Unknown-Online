namespace CasualtiesUnknownOnline.Runtime.GameAdapter;

/// <summary>
/// The Online UI's read-only window into a remote player's inventory: it opens
/// the game's own native radial backpack on that player's render clone. The
/// clone is never the authority and no item mutation happens through it.
/// </summary>
public interface IRemoteInventoryPresentation
{
	/// <summary>
	/// Opens the game's native radial backpack UI focused on one in-world remote
	/// player's render clone. Returns false when no session/world/remote clone is
	/// available yet. The view is read-only presentation; the clone is never the
	/// authority and no item mutation is performed through it.
	/// </summary>
	bool OpenRemoteBackpack(ulong targetSteamId, string displayName);
}
