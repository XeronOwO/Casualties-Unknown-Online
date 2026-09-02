namespace CasualtiesUnknownOnline.Runtime.Session.Mods;

/// <summary>
/// The Runtime → Game Adapter boundary for mod tile/block placement. The
/// Runtime defines the contract (plus permission/session/policy); the Game
/// Adapter resolves the stable tile content id to its custom block index,
/// prepares the tile in the current world palette, and calls the vanilla
/// <c>WorldGeneration.SetBlock</c> path. The existing <c>BlockPlaced</c>
/// channel replicates the write; this seam only performs the local placement.
/// </summary>
public interface IModTilePlacer
{
	/// <summary>
	/// Place one custom tile at integer block coordinates. Returns true only
	/// when the tile id is known, the custom tile is ready in the current world
	/// palette, and <c>SetBlock</c> was called. The caller (ModService) is
	/// responsible for permission/session/policy gating.
	/// </summary>
	bool TryPlaceBlock(string tileId, int x, int y);
}
