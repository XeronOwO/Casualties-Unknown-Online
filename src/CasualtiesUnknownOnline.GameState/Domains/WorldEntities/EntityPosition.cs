using System;

namespace CasualtiesUnknownOnline.GameState.Domains.WorldEntities;

/// <summary>
/// Integer world-cell identity for deterministically generated world entities.
/// Floating point positions are rounded to the cell, matching the position-keyed
/// registries used by the live world (sub-unit drift is noise).
/// </summary>
public readonly record struct EntityPosition(int X, int Y)
{
	public static EntityPosition FromWorld(float x, float y) =>
		new((int)Math.Floor(x), (int)Math.Floor(y));

	public float CenterX => X + 0.5f;

	public float CenterY => Y + 0.5f;
}
