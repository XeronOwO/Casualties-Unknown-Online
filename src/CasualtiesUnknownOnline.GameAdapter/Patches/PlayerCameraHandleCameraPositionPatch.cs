using CasualtiesUnknownOnline.GameAdapter.World;
using HarmonyLib;
using UnityEngine;

namespace CasualtiesUnknownOnline.GameAdapter.Patches;

/// <summary>
/// The spectator's camera target, applied where 1.0 computes the camera position.
///
/// <para>
/// The demo's camera followed <c>PlayerCamera.following</c> when that field was set; 1.0 removed the field and the
/// camera now reads <c>this.body.transform.position</c> directly (<c>PlayerCamera.cs:2144</c>). The spectator still
/// has to look at the WATCHED body, so this prefix skips the game's own computation while a watch target is
/// registered and writes the same position the game would write for that body (z = -3). Releasing the watch clears
/// the target and the game's own computation runs again.
/// </para>
/// </summary>
[HarmonyPatch(typeof(PlayerCamera), "HandleCameraPosition")]
internal static class PlayerCameraHandleCameraPositionPatch
{
	private static bool Prefix(PlayerCamera __instance)
	{
		var target = SpectatorCameraService.WatchTarget;
		if (target == null) // Unity object — ==
		{
			return true;
		}

		var position = target.position;
		__instance.transform.position = new Vector3(position.x, position.y, -3f);
		return false;
	}
}
