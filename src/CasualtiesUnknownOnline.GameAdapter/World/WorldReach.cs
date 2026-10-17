namespace CasualtiesUnknownOnline.GameAdapter.World;

/// <summary>
/// How far a player's own hand reaches, in WORLD UNITS. Named for the unit on purpose: the withdrawn block bound compared
/// a block INDEX against a world position and refused every legitimate break, and a constant called "cells" is how that
/// mistake hides. One number for every gesture bound, so a hand that can pick a lock is also one that can break the wall.
/// </summary>
internal static class WorldReach
{
	/// <summary>The furthest a player's own gesture may affect a world point.</summary>
	internal const float WorldUnits = 8f;
}
