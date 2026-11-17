using UnityEngine;

namespace CasualtiesUnknownOnline.GameAdapter.World;

/// <summary>
/// One building, found the way the restore needs it: the cell the cut recorded, never "whatever
/// happens to be on screen". The point query is the exact lookup the native hit tests use; the
/// fallback covers a building whose chunk is deactivated (far from the player, or mid-restore),
/// which <see cref="Physics2D.OverlapPoint(Vector2)"/> skips while the object still exists
/// (2026-11-18: a restored health row at (8.5, 492.5) was refused for a building the regenerated
/// layer had, in an inactive chunk).
/// </summary>
internal static class BuildingEntityLookup
{
	/// <summary>How far a building may sit from the recorded point and still be that building: the kernel records a cell centre, the entity stands on its own transform, and a regenerated layer can place the same building a cell off (2026-11-18: a row at (18.5, 490.5) named a building the layer put 2 m away).</summary>
	private const float Tolerance = 3f;

	internal static BuildingEntity? At(float x, float y)
	{
		var point = new Vector2(x, y);
		if (Physics2D.OverlapPoint(point) is { } hit && hit.GetComponent<BuildingEntity>() is { } onThePoint)
		{
			return onThePoint;
		}

		BuildingEntity? nearest = null;
		var best = Tolerance * Tolerance;
		foreach (var candidate in Object.FindObjectsOfType<BuildingEntity>(true))
		{
			var offset = (Vector2)candidate.transform.position - point;
			var distance = offset.sqrMagnitude;
			if (distance <= best)
			{
				best = distance;
				nearest = candidate;
			}
		}

		return nearest;
	}
}
