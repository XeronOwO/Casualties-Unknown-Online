using System.Collections.Generic;

namespace CasualtiesUnknownOnline.Runtime.Session.World;

/// <summary>
/// The position-keyed identity rule every trap/mechanism lookup uses: the entity CLOSEST to
/// the event point within a tolerance, never merely the first one the scene returns.
/// <para>
/// The distinction is not cosmetic. A world can hold several entities of one type within the
/// tolerance (two mines in one cell, a crystal beside a bush, a geyser whose child sits in the
/// neighbouring cell), and "first" then depends on the engine's enumeration order. The
/// entity-event appliers use the answer as the entity's IDENTITY — the trap the event applies
/// to, and (for a destructive event) the table its reported drops are judged against — so a
/// first hit can apply an event to a sibling and judge a legitimate roll against the wrong
/// prefab's table. The old 3-unit first hit did exactly that in the runtime-entity geyser case
/// (<c>RuntimeEntityMatchTests.FindIndex_ADeferredGeyserReportBindsItsOwnCopyNotTheNearestSameCellOne</c>).
/// </para>
/// </summary>
internal static class NearestCandidate
{
	/// <summary>
	/// Index of the candidate nearest (<paramref name="x"/>, <paramref name="y"/>) within
	/// <paramref name="tolerance"/>, or -1 when none is inside it. The boundary is EXCLUDED, the
	/// rule the appliers' <c>Distance(...) &lt; tolerance</c> has always used; a tie keeps the
	/// earlier candidate, so one scene answers the same index twice.
	/// </summary>
	internal static int FindIndex(float x, float y, IReadOnlyList<(float X, float Y)> candidates, float tolerance)
	{
		var best = -1;
		var bestSquared = tolerance * tolerance;
		for (var i = 0; i < candidates.Count; i++)
		{
			var dx = candidates[i].X - x;
			var dy = candidates[i].Y - y;
			var squared = (dx * dx) + (dy * dy);
			if (squared < bestSquared)
			{
				bestSquared = squared;
				best = i;
			}
		}

		return best;
	}
}
