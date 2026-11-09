using System.Collections;
using CasualtiesUnknownOnline.GameState.Domains.World;
using CasualtiesUnknownOnline.Runtime.Session.World;

namespace CasualtiesUnknownOnline.GameAdapter.Patches;

/// <summary>The shared decision both ending patches run — see <see cref="MercEndingPatch"/>.</summary>
internal static class EndingBranch
{
	private static bool _replaying;

	internal static bool Redirect(WorldGeneration world, RunEnding local, ref IEnumerator result)
	{
		if (_replaying)
		{
			return true; // the replay call itself — let the game's own method run
		}

		if (RunEndingTracker.FromAuthority && RunEndingTracker.Current != local)
		{
			_replaying = true;
			try
			{
				result = RunEndingTracker.Current == RunEnding.Merc
					? world.MercEnding()
					: world.SurfaceBreachCutscene();
			}
			finally
			{
				_replaying = false;
			}

			return false; // the host's branch is what the caller starts
		}

		RunEndingTracker.Record(local); // this client resolved the branch (the host)
		return true;
	}
}
