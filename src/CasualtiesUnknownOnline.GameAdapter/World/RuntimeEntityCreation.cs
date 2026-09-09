using UnityEngine;

namespace CasualtiesUnknownOnline.GameAdapter.World;

/// <summary>
/// The creation identity stamped onto every runtime-created BuildingEntity (the
/// entity-spawn channel's own record): prefab id + the floored CREATION cell —
/// the position the live report carried, not the entity's current position.
/// <para>
/// The recovery tables key by that creation cell, but a BuildingEntity's
/// Rigidbody2D becomes Dynamic while its chunk is visible
/// (BuildingEntity.cs:54), so a runtime creation can fall or be pushed across
/// cells before it dies. The death funnel therefore reads this marker and
/// reports the CREATION key, not <c>transform.position</c> — otherwise the
/// record would never be dropped, and the 60 s re-broadcast would resurrect the
/// dead entity. Stamped by <see cref="EntitySpawnSync"/> on the local report
/// path and on every remotely created copy.
/// </para>
/// </summary>
public sealed class RuntimeEntityCreation : MonoBehaviour
{
	public string Id { get; set; } = string.Empty;

	public int CellX { get; set; }

	public int CellY { get; set; }

	/// <summary>Add or refresh the marker on an entity (a repeat report refreshes the same creation's key).</summary>
	internal static void Stamp(BuildingEntity entity, string id, float x, float y)
	{
		if (string.IsNullOrEmpty(id))
		{
			return;
		}

		var marker = entity.GetComponent<RuntimeEntityCreation>();
		if (marker == null) // Unity object — ==
		{
			marker = entity.gameObject.AddComponent<RuntimeEntityCreation>();
		}

		marker.Id = id;
		marker.CellX = (int)Mathf.Floor(x);
		marker.CellY = (int)Mathf.Floor(y);
	}

	/// <summary>Read the marker; false for a generated entity (it never entered the runtime-creation tables).</summary>
	internal static bool TryRead(BuildingEntity entity, out string id, out int cellX, out int cellY)
	{
		var marker = entity.GetComponent<RuntimeEntityCreation>();
		if (marker == null || string.IsNullOrEmpty(marker.Id)) // Unity object — ==
		{
			id = string.Empty;
			cellX = 0;
			cellY = 0;
			return false;
		}

		id = marker.Id;
		cellX = marker.CellX;
		cellY = marker.CellY;
		return true;
	}
}
