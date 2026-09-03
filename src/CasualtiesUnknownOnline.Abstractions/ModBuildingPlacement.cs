namespace CasualtiesUnknownOnline.Abstractions;

/// <summary>
/// Surface type a custom building entity attaches to when placed by automatic
/// world generation. Plain data in Abstractions; the Game Adapter translates it
/// to the vanilla raycast direction.
/// </summary>
public enum ModBuildingPlacement
{
	/// <summary>Places the entity on the floor.</summary>
	Floor = 0,

	/// <summary>Places the entity on the ceiling.</summary>
	Ceiling = 1,

	/// <summary>Places the entity on a wall.</summary>
	Wall = 2
}
