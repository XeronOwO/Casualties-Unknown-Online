namespace CasualtiesUnknownOnline.Abstractions;

/// <summary>
/// The mod multi-block structure placement surface. It lets a synchronized or
/// authoritative mod place a static structure registered through
/// <see cref="IModContent"/> (<see cref="ModContentKind.Structure"/>) at integer
/// block coordinates. The Game Adapter resolves every non-air cell to either a
/// vanilla block index or a custom tile content id, calls the vanilla
/// <c>WorldGeneration.SetBlock</c> path for each cell, and reuses the existing
/// <c>BlockPlaced</c> channel for replication. The mod never touches Unity or
/// game-assembly types.
///
/// Placement requires <see cref="ModPermission.SpawnEntity"/> — the same
/// host/state permission family as entity, item, and single-tile placement.
/// All cells must land inside the current world and on air; the Game Adapter
/// performs a full preflight before the first write, so a failed request never
/// leaves a partial structure.
/// </summary>
public interface IModStructurePlacement
{
	/// <summary>
	/// True when this mod copy declares <see cref="ModPermission.SpawnEntity"/>.
	/// Every placement call also checks and logs this before acting.
	/// </summary>
	bool CanPlace { get; }

	/// <summary>
	/// Try to place one registered structure at integer block coordinates.
	/// Returns false (with a framework log) when the mod lacks
	/// <see cref="ModPermission.SpawnEntity"/>, the session is not active or the
	/// local player is not in a world, the structure id is unknown/failed, a
	/// referenced custom tile is not available, any target block is not air or
	/// outside the world, or the Game Adapter cannot complete the placement.
	/// </summary>
	bool TryPlaceStructure(string structureId, int originX, int originY);
}
