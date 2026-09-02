namespace CasualtiesUnknownOnline.Runtime.Session.Mods;

/// <summary>
/// The Runtime → Game Adapter boundary for mod multi-block structure placement.
/// The Runtime defines the contract (plus permission/session/policy); the Game
/// Adapter resolves the structure content id to its compiled cells, prepares any
/// referenced custom tiles in the current world palette, validates every target
/// cell, and calls the vanilla <c>WorldGeneration.SetBlock</c> path per cell.
/// The existing <c>BlockPlaced</c> channel replicates each write; this seam only
/// performs the local placement.
/// </summary>
public interface IModStructurePlacer
{
	/// <summary>
	/// Place one registered structure at integer block coordinates. Returns true
	/// only when the structure id is known, every referenced custom tile is
	/// ready, every target cell is inside the world and on air, and
	/// <c>SetBlock</c> was called for every non-air cell. The caller
	/// (ModService) is responsible for permission/session/policy gating.
	/// </summary>
	bool TryPlaceStructure(string structureId, int originX, int originY);
}
