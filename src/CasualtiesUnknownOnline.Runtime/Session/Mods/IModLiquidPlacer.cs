namespace CasualtiesUnknownOnline.Runtime.Session.Mods;

/// <summary>
/// The Runtime → Game Adapter boundary for mod liquid-tile placement. The
/// Runtime defines the contract (plus permission/session/policy); the Game
/// Adapter resolves the stable liquid-tile content id to its deterministic
/// custom world-fluid byte, validates the world/solid-cell/host-authority
/// conditions, and calls the vanilla <c>FluidManager.SetLiquid</c> /
/// <c>StartFill</c> path. The existing host fluid stream replicates the grid
/// write; this seam only performs the local host/solo placement.
/// </summary>
public interface IModLiquidPlacer
{
	/// <summary>
	/// Place one custom liquid-tile cell at integer block coordinates. Returns
	/// true only when the liquid-tile id is known/mapped, the world and fluid
	/// manager are available, the target is inside the world and empty, this
	/// process owns the fluid authority, and <c>SetLiquid</c> was called. The
	/// caller (ModService) is responsible for permission/session/policy gating.
	/// </summary>
	bool TryPlaceLiquid(string liquidTileId, int x, int y);

	/// <summary>
	/// Start a flood fill of one custom liquid-tile definition from an integer
	/// block seed. Returns true only when the same host-authority/availability
	/// checks pass and <c>StartFill</c> was called. A <c>maxFill</c> value less
	/// than or equal to zero lets the Game Adapter use the definition's authored
	/// <c>MaxFloodFill</c> cap.
	/// </summary>
	bool TryFloodFill(string liquidTileId, int startX, int startY, int maxFill);
}
