using System.Collections.Generic;
using System.Linq;
using CasualtiesUnknownOnline.Runtime.Session.World;
using HarmonyLib;
using Microsoft.Extensions.Logging;
using UnityEngine;
using ILogger = Microsoft.Extensions.Logging.ILogger;

namespace CasualtiesUnknownOnline.GameAdapter.World;

/// <summary>
/// Fires the mines the host's own blast drove below their firing line INSIDE the blast's apply scope, instead of
/// letting each one wait for its <c>OnDestroy</c> (<c>MineScript.cs</c>: `if (this.build.health &lt; 0.5f &amp;&amp;
/// !this.exploded)`), which runs after the scope has closed.
///
/// <para>
/// Why it matters: a chained mine's blast damages buildings of its own, and outside the scope those victims are
/// neither captured as the event's health entries nor marked as remote deaths, so the host's own death branch rolls a
/// second drop set for a death the reporting member already rolled (ticket
/// `host-blast-rolls-a-second-drop-set.md`, slice 2). Replaying the chain here keeps it in the same capture: every
/// victim it damages lands in the scope through the ordinary health seam, which is also where the remote-death mark
/// happens.
/// </para>
///
/// <para>
/// The mine is marked FIRED before its explosion runs, because the replay is the firing: setting the same flag the
/// native destroy reads is what stops the later <c>OnDestroy</c> from exploding it a second time. The flag is private
/// in the game's code, which is why it is written through <see cref="Traverse"/> rather than a cast.
/// </para>
/// </summary>
internal static class ChainedBlastReplay
{
	/// <summary>Explodes every damaged mine that has not fired yet, inside the caller's scope.</summary>
	internal static int Replay(IReadOnlyList<BuildingEntity> damaged, ILogger? log)
	{
		var replayed = 0;

		// The replay damages more entities, which the scope records as it happens: iterate a snapshot, so the chain's
		// own victims are captured by the scope but not re-entered here (their own mines fire through this same pass
		// only if they were in the snapshot, and a chain of chains is a shape this game's damage numbers make rare
		// enough to leave to the next frame's destroy, which still explodes them).
		foreach (var building in damaged.ToArray())
		{
			if (building == null) // Unity object — ==
			{
				continue;
			}

			var mine = building.GetComponent<MineScript>();
			if (mine == null) // Unity object — ==
			{
				continue;
			}

			var exploded = Traverse.Create(mine).Field("exploded").GetValue<bool>();
			if (!ChainedBlastPolicy.ShouldReplay(hasMineScript: true, building.health, exploded))
			{
				continue;
			}

			Traverse.Create(mine).Field("exploded").SetValue(true);
			WorldGeneration.CreateExplosion(new ExplosionParams
			{
				position = building.transform.position + Vector3.up,
			});
			replayed++;
		}

		if (replayed > 0)
		{
			log?.LogInformation("[TrapBlast] replayed {Count} chained mine blast(s) inside the apply scope, so their victims are captured and marked instead of rolling a second set later.", replayed);
		}

		return replayed;
	}
}
