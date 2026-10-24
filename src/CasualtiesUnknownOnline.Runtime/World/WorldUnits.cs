namespace CasualtiesUnknownOnline.Runtime.World;

/// <summary>
/// The game's own world-unit scale, in one place so a CUO label can never disagree with the game's numbers.
///
/// <para>
/// The game reports DEPTH in "metres" by multiplying world units by <c>0.3f</c>
/// (<c>WorldGeneration.cs</c>: `return -(this.body.transform.position.y - this.halfHeight) * 0.3f;` for the player's
/// depth and the same factor for the radiation line's), and converts the other way where it accumulates distance
/// (`this.totalTraveled / 0.3f`). So one world unit is 30 cm, and a layer is 2 × halfHeight units: with the shipped
/// <c>halfHeight</c> of 512 that is 1024 units, i.e. the 307 m a player reads as "one layer is about 300 m".
/// </para>
///
/// <para>
/// The user's 2026-10-24 finding: CUO's member nameplate printed the raw world-unit distance with the catalogue's
/// "{0} m" line, so two players one layer apart read as "1000 m" instead of "307 m". The conversion belongs here rather
/// than in the UI so every surface that shows a distance uses the game's scale.
/// </para>
/// </summary>
public static class WorldUnits
{
	/// <summary>How many of the game's "metres" one world unit is: the <c>0.3f</c> factor of its own depth formula.</summary>
	public const float MetersPerUnit = 0.3f;

	/// <summary>World units to the game's displayed metres.</summary>
	public static float ToMeters(float worldUnits) => worldUnits * MetersPerUnit;
}
