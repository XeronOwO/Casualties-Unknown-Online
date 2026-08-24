using System.Collections.Generic;

namespace CasualtiesUnknownOnline.Runtime.OnlineUi;

/// <summary>
/// Pure hit-test for the in-world right-click player menu. The previous
/// implementation returned the first matching remote player; this picker
/// returns every candidate inside the click radius ordered by distance, so the
/// context menu can present an explicit selector when several remote players
/// overlap on screen.
/// </summary>
public static class RemoteTargetPicker
{
	/// <summary>
	/// Returns all candidates within <paramref name="radius"/> of the mouse in
	/// GUI coordinates, nearest first. Ties are broken by SteamId so the result
	/// is deterministic.
	/// </summary>
	public static IReadOnlyList<RemoteScreenTarget> Find(
		IReadOnlyList<RemoteScreenTarget> candidates,
		float mouseX,
		float mouseY,
		float radius)
	{
		if (candidates is null || candidates.Count == 0 || radius < 0f)
		{
			return [];
		}

		var matches = new List<RemoteScreenTarget>();
		var radiusSquared = radius * radius;
		foreach (var candidate in candidates)
		{
			var dx = candidate.X - mouseX;
			var dy = candidate.Y - mouseY;
			var distanceSquared = (dx * dx) + (dy * dy);
			if (distanceSquared <= radiusSquared)
			{
				matches.Add(candidate);
			}
		}

		matches.Sort((a, b) =>
		{
			var byDistance = DistanceSquared(a, mouseX, mouseY).CompareTo(DistanceSquared(b, mouseX, mouseY));
			return byDistance != 0 ? byDistance : a.SteamId.CompareTo(b.SteamId);
		});
		return matches;
	}

	private static float DistanceSquared(RemoteScreenTarget target, float mouseX, float mouseY)
	{
		var dx = target.X - mouseX;
		var dy = target.Y - mouseY;
		return (dx * dx) + (dy * dy);
	}
}
