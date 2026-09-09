using CasualtiesUnknownOnline.Runtime.Session.World;
using UnityEngine;

namespace CasualtiesUnknownOnline.GameAdapter.World;

/// <summary>
/// The creation identity stamped onto every runtime-created BuildingEntity (the
/// entity-spawn channel's own record): the full <see cref="RuntimeEntityKey"/>
/// — prefab id + the floored CREATION cell + the creation-instance token.
/// <para>
/// The recovery tables key by that identity, but a BuildingEntity's
/// Rigidbody2D becomes Dynamic while its chunk is visible
/// (BuildingEntity.cs:54), so a runtime creation can fall or be pushed across
/// cells before it dies. The death funnel therefore reads this marker and
/// reports the CREATION key, not <c>transform.position</c> — otherwise the
/// record would never be dropped, and the 60 s re-broadcast would resurrect the
/// dead entity. The token half is what keeps two identical prefabs created in
/// one cell apart, so a re-report or a death can never be attributed to the
/// sibling copy. Stamped by <see cref="EntitySpawnSync"/> on the local report
/// path and on every remotely created copy.
/// </para>
/// </summary>
public sealed class RuntimeEntityCreation : MonoBehaviour
{
	public string Id { get; set; } = string.Empty;

	public int CellX { get; set; }

	public int CellY { get; set; }

	public ulong CreatorSteamId { get; set; }

	public uint CreationSequence { get; set; }

	/// <summary>Add or refresh the marker on an entity (a repeat record refreshes the same creation's key).</summary>
	internal static void Stamp(BuildingEntity entity, RuntimeEntityKey key)
	{
		if (string.IsNullOrEmpty(key.Id))
		{
			return;
		}

		var marker = entity.GetComponent<RuntimeEntityCreation>();
		if (marker == null) // Unity object — ==
		{
			marker = entity.gameObject.AddComponent<RuntimeEntityCreation>();
		}

		marker.Id = key.Id;
		marker.CellX = key.X;
		marker.CellY = key.Y;
		marker.CreatorSteamId = key.CreatorSteamId;
		marker.CreationSequence = key.CreationSequence;
	}

	/// <summary>Read the marker; false for an entity that never entered the runtime-creation tables.</summary>
	internal static bool TryRead(BuildingEntity entity, out RuntimeEntityKey key)
	{
		var marker = entity == null ? null : entity.GetComponent<RuntimeEntityCreation>(); // Unity object — ==
		if (marker == null || string.IsNullOrEmpty(marker.Id)) // Unity object — ==
		{
			key = default;
			return false;
		}

		key = new RuntimeEntityKey(marker.Id, marker.CellX, marker.CellY, marker.CreatorSteamId, marker.CreationSequence);
		return true;
	}

	/// <summary>
	/// Read the creation marker belonging to the entity a component lives on —
	/// walking up the transform chain. The marker is stamped on the entity's own
	/// GameObject, while some trap scripts hang on a CHILD of it
	/// (<c>GeyserScript.cs:13</c> reads <c>transform.parent</c>), so a
	/// same-GameObject lookup would miss those. The walk returns the FIRST
	/// ancestor carrying the marker: a component nested inside a markerless
	/// child of a runtime-created entity still resolves to that entity, while a
	/// component on an unrelated object resolves to nothing (its own chain has
	/// no marker).
	/// </summary>
	internal static bool TryReadOnEntityOf(Component component, out RuntimeEntityKey key)
	{
		key = default;
		if (component == null) // Unity object — ==
		{
			return false;
		}

		for (var transform = component.transform; transform != null; transform = transform.parent) // Unity object — ==
		{
			if (TryRead(transform.GetComponent<BuildingEntity>(), out key))
			{
				return true;
			}
		}

		return false;
	}
}
