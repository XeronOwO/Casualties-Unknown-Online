using System.Collections.Generic;
using System.Linq;
using Object = UnityEngine.Object;

namespace CasualtiesUnknownOnline.GameAdapter.Character;

/// <summary>
/// The ONE answer to "which animals exist on this side right now": the TRACKED
/// set (every animal a Start reported, plus the ones an earlier union wrote back)
/// united with a live scene scan (an animal the game instantiated whose Start has
/// not run yet, and every copy that survives a roster reset while the tracked set
/// does not). The host's capture, the generation-finished edge pass and the
/// guest's freeze/adoption candidates all read this union — a second notion of
/// "the local animals" is how a reset emptied the adoption candidates and made a
/// 956-row world build 946 copies (batch `20261021-f`, row 1).
/// <para>
/// The scan is the ACTIVE scene: an object deactivated for destruction is
/// deliberately outside it (see <c>EnemyRosterBinder.DestroyCopy</c>), and an
/// inactive animal the generation left behind would not be seen at the edge — no
/// such animal was observed, and the batch reads the edge count.
/// </para>
/// </summary>
internal static class EnemyAnimalScan
{
	/// <summary>Every animal in the scene right now. One frame's scan, never a long-lived list: the game destroys and creates these objects freely.</summary>
	internal static List<BuildingEntity> InScene() =>
		[.. Object.FindObjectsOfType<BuildingEntity>().Where(static entity => entity.animal)];

	/// <summary>
	/// The tracked set ∪ a live scene scan, with the destroyed entries pruned and
	/// the survivors recorded in <paramref name="tracked"/> so the next pass starts
	/// from the union. The scan is the safety net for a <c>Start</c> that has not
	/// run yet and for a set some boundary cleared while the copies are still in
	/// the scene.
	/// </summary>
	internal static List<BuildingEntity> Union(HashSet<BuildingEntity> tracked)
	{
		tracked.RemoveWhere(static entity => entity == null); // Unity object — ==
		var union = new HashSet<BuildingEntity>(tracked);
		foreach (var entity in InScene())
		{
			union.Add(entity);
		}

		foreach (var entity in union)
		{
			tracked.Add(entity);
		}

		return [.. union];
	}
}
