namespace CasualtiesUnknownOnline.Runtime.Session.Mods;

/// <summary>
/// The non-Unity validation rails for the mod entity-spawn surface. The actual
/// prefab existence / building-entity shape is verified by the Game Adapter;
/// this policy rejects malformed request values before they cross the seam and
/// keeps the public API from being a vector for weird invocations.
/// </summary>
public static class ModEntitySpawnPolicy
{
	public const int MaxPrefabIdLength = 128;

	/// <summary>True for a non-empty, non-control-character prefab id within the cap.</summary>
	public static bool IsValidPrefabId(string prefabId)
	{
		if (string.IsNullOrWhiteSpace(prefabId) || prefabId.Length > MaxPrefabIdLength)
		{
			return false;
		}

		foreach (var ch in prefabId)
		{
			if (char.IsControl(ch))
			{
				return false;
			}
		}

		return true;
	}

	/// <summary>True when both coordinates are finite numbers (no NaN/infinity).</summary>
	public static bool IsValidPosition(float x, float y) =>
		!float.IsNaN(x) && !float.IsInfinity(x)
		&& !float.IsNaN(y) && !float.IsInfinity(y);

	/// <summary>True when the rotation is a finite number (no NaN/infinity).</summary>
	public static bool IsValidRotation(float rotation) =>
		!float.IsNaN(rotation) && !float.IsInfinity(rotation);
}
