using CasualtiesUnknownOnline.GameAdapter.Character;
using HarmonyLib;

namespace CasualtiesUnknownOnline.GameAdapter.Patches;

/// <summary>
/// Enemy-bite detection: the bite is the game's own <c>SpiderHandler.DamageLimb</c>
/// (local compute — the victim's body is already damaged when this runs). The
/// postfix reports the post-bite limb/body state so it travels as the dedicated
/// <c>EnemyBite</c> event, never the 1 Hz snapshot. A render clone's limb is
/// skipped — its vitals are discarded with the clone, never synced.
/// SpiderHandlerTBE overrides DamageLimb (dismember/disfigure path), so it
/// needs its own patch — a Harmony patch on a base method does not cover an
/// override.
/// </summary>
internal static class EnemyBitePatches
{
	[HarmonyPatch(typeof(SpiderHandler), "DamageLimb")]
	internal static class SpiderHandlerDamageLimbPatch
	{
		private static void Postfix(Limb limb) => Report(limb);
	}

	[HarmonyPatch(typeof(SpiderHandlerTBE), "DamageLimb")]
	internal static class SpiderHandlerTBEDamageLimbPatch
	{
		private static void Postfix(Limb limb) => Report(limb);
	}

	private static void Report(Limb limb)
	{
		if (limb == null || limb.body == null) // Unity object — ==
		{
			return;
		}

		if (limb.body.GetComponentInParent<RemoteBodyDriver>() != null)
		{
			return; // a remote render clone — its damage is never synced
		}

		PatchBridge.Impl?.OnEnemyBite(limb);
	}
}
