using HarmonyLib;

namespace CasualtiesUnknownOnline.GameAdapter.Patches;

/// <summary>
/// The spawn landing camera shake (WorldGeneration.cs:3673 — "LifePodShake",
/// scheduled via Invoke with REAL time 1 s after generation finished): while
/// the start gate holds (timeScale = 0) the shake fires into the frozen
/// world — the player never sees it, and it is gone by the time the gate
/// releases ("no landing animation"). Defer it like the landing sound: the
/// gate release (or the no-gate fallback) replays it.
/// </summary>
[HarmonyPatch(typeof(PlayerCamera), "LifePodShake")]
internal static class LifePodShakePatch
{
	private static bool Prefix()
	{
		if (PatchBridge.Impl is { IsSessionActive: true, IsWaitingForReady: true })
		{
			PatchBridge.Impl?.DeferLifePodShake();
			return false; // the start gate holds — replay at release
		}

		return true;
	}
}
