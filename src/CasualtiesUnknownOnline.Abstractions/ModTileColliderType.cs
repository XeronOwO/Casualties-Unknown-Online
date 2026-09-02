namespace CasualtiesUnknownOnline.Abstractions;

/// <summary>
/// Stable tile collider names for <see cref="ModTileDefinition.ColliderType"/>.
/// The values are plain enums so Abstractions stays free of Unity types; the
/// Game Adapter maps them to the vanilla <c>Tile.ColliderType</c> enum.
/// </summary>
public enum ModTileColliderType
{
	/// <summary>The tile has no collider.</summary>
	None = 0,

	/// <summary>A single sprite-shaped collider.</summary>
	Sprite = 1,

	/// <summary>A grid-aligned collider (the vanilla default for terrain tiles).</summary>
	Grid = 2
}
