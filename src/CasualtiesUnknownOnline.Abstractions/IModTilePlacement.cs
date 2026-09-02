namespace CasualtiesUnknownOnline.Abstractions;

/// <summary>
/// The mod tile/block placement surface. It lets a synchronized or
/// authoritative mod place one static terrain tile at integer block
/// coordinates. The tile is addressed by the stable content id registered
/// through <see cref="IModContent"/>; the Game Adapter resolves it to the
/// deterministic custom block index and calls the vanilla
/// <c>WorldGeneration.SetBlock</c> path. The target cell must be air,
/// matching the existing <c>BlockPlaced</c> arbitration rule. CUO reuses the
/// existing <c>BlockPlaced</c> channel, so every side applies the same block
/// and the mod never touches Unity or game-assembly types.
///
/// Placement requires <see cref="ModPermission.SpawnEntity"/>: the same
/// host/state permission family as entity and item spawning. It supports
/// custom tiles registered as <see cref="ModContentKind.Tile"/>; vanilla
/// blocks are not addressable by this seam until/unless a vanilla id table is
/// exposed.
/// </summary>
public interface IModTilePlacement
{
	/// <summary>
	/// True when this mod copy declares <see cref="ModPermission.SpawnEntity"/>.
	/// Every placement call also checks and logs this before acting.
	/// </summary>
	bool CanPlace { get; }

	/// <summary>
	/// Try to place one custom tile at integer block coordinates. Returns false
	/// (with a framework log) when the mod lacks
	/// <see cref="ModPermission.SpawnEntity"/>, the session is not active or the
	/// local player is not in a world, the tile id fails the request rails, the
	/// tile definition is unknown/failed, the target block is not air, or the
	/// Game Adapter cannot prepare the custom tile in the current world palette.
	/// </summary>
	bool TryPlaceBlock(string tileId, int x, int y);
}
