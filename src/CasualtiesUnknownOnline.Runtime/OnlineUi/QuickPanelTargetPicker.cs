using System.Collections.Generic;

namespace CasualtiesUnknownOnline.Runtime.OnlineUi;

/// <summary>
/// Pure target selection for the standalone player-interaction quick panel.
/// The panel keeps its previously chosen target while that player is still an
/// in-world remote; when the target is gone or no target was chosen yet, the
/// nearest in-world remote wins (ties by SteamId for determinism).
/// </summary>
public static class QuickPanelTargetPicker
{
	public static ulong? Resolve(
		ulong? current,
		float localX,
		float localY,
		IReadOnlyList<QuickPanelTargetCandidate> candidates)
	{
		if (candidates is null || candidates.Count == 0)
		{
			return null;
		}

		if (current is { } currentId)
		{
			foreach (var candidate in candidates)
			{
				if (candidate.SteamId == currentId)
				{
					return currentId;
				}
			}
		}

		ulong bestId = 0;
		var bestDistanceSquared = float.PositiveInfinity;
		foreach (var candidate in candidates)
		{
			var dx = candidate.X - localX;
			var dy = candidate.Y - localY;
			var distanceSquared = (dx * dx) + (dy * dy);
			if (distanceSquared < bestDistanceSquared
				|| (distanceSquared == bestDistanceSquared && candidate.SteamId < bestId))
			{
				bestId = candidate.SteamId;
				bestDistanceSquared = distanceSquared;
			}
		}

		return bestId == 0 ? null : bestId;
	}
}
