using System.Globalization;
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

	/// <summary>
	/// How wide the neighbourhood is when a row's own building is absent: the question is whether the layer
	/// produced that AREA (a building of its stands nearby) or has not instantiated it at all. The measurement
	/// that set it: on 2026-11-18 one refusal in five fresh runs named a building the layer did not have, with
	/// another building 9.3 m away — the area existed, the building did not (ticket
	/// <c>docs/backlog/todo/1.0-cut-outlives-a-felled-building.md</c>).
	/// </summary>
	internal const float NeighbourhoodRadius = 25f;

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

	internal static bool AnyWithin(float x, float y, float radius)
	{
		var point = new Vector2(x, y);
		var limit = radius * radius;
		foreach (var candidate in Object.FindObjectsOfType<BuildingEntity>(true))
		{
			var offset = (Vector2)candidate.transform.position - point;
			if (offset.sqrMagnitude <= limit)
			{
				return true;
			}
		}

		return false;
	}

	/// <summary>
	/// A sentence for a refusal: where the nearest building stands, or that the layer has none. The measurement
	/// needs the distance, not just "no entity there" — a positive-health row was refused and the log could not say
	/// whether the building was absent or a few metres off (2026-11-18, ticket
	/// <c>docs/backlog/todo/1.0-cut-outlives-a-felled-building.md</c>).
	/// </summary>
	internal static string Nearest(float x, float y)
	{
		var point = new Vector2(x, y);
		BuildingEntity? nearest = null;
		var best = float.MaxValue;
		foreach (var candidate in Object.FindObjectsOfType<BuildingEntity>(true))
		{
			var offset = (Vector2)candidate.transform.position - point;
			var distance = offset.sqrMagnitude;
			if (distance < best)
			{
				best = distance;
				nearest = candidate;
			}
		}

		if (nearest is null)
		{
			return "the layer has no building anywhere";
		}

		var position = (Vector2)nearest.transform.position;
		var distanceText = Mathf.Sqrt(best).ToString("F1", CultureInfo.InvariantCulture);
		var xText = position.x.ToString("F1", CultureInfo.InvariantCulture);
		var yText = position.y.ToString("F1", CultureInfo.InvariantCulture);
		return "the nearest building stands " + distanceText + " m away at (" + xText + "," + yText + ")";
	}
}
