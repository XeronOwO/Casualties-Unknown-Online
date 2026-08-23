using System.Collections.Generic;

namespace CasualtiesUnknownOnline.Runtime.Session.EntitySync;

/// <summary>
/// Engine-agnostic layer-progress fact for the host-side radiation-line
/// straggler rule. The Game Adapter gathers the local + remote entity-stream
/// players; the pure policy below makes the activation decision.
/// </summary>
public readonly struct RadiationPlayerProgress(float y, bool alive)
{
	/// <summary>World-space Y of the player's body.</summary>
	public readonly float Y = y;

	/// <summary>True when the player is alive (dead/left-world players are not stragglers).</summary>
	public readonly bool Alive = alive;
}

/// <summary>
/// Pure host-side rule for co-op radiation-line straggler pressure. The
/// vanilla game starts its line from the single-player layer timer
/// (WorldGeneration.cs:859-863); in a co-op session that would only reflect
/// the host's own progress. This policy is the multiplayer extension: once at
/// least one living player has reached the layer bottom and another living
/// player is still above it, the host activates the line so the stragglers
/// feel the same radiation pressure the original mechanic applies.
///
/// The line is one-way in the vanilla game (it stays active until the layer is
/// regenerated), so this policy only decides when to ACTIVATE it. The actual
/// per-player body radiation/eye effects already run in each player's local
/// <c>RadiationLine.Update</c>; no new per-player pressure message is needed.
/// </summary>
public static class RadiationStragglerPolicy
{
	/// <summary>
	/// True when at least one living player has reached the layer bottom
	/// (<paramref name="layerBottomY"/>) and at least one other living player
	/// is still above it. Dead players are ignored.
	/// </summary>
	public static bool ShouldActivateLine(IEnumerable<RadiationPlayerProgress> players, float layerBottomY)
	{
		var atBottom = 0;
		var above = 0;

		foreach (var player in players)
		{
			if (!player.Alive)
			{
				continue;
			}

			if (player.Y < layerBottomY)
			{
				atBottom++;
			}
			else
			{
				above++;
			}
		}

		return atBottom > 0 && above > 0;
	}
}
