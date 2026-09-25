using HarmonyLib;

namespace CasualtiesUnknownOnline.GameAdapter.Patches;

/// <summary>
/// The remote backpack view is read-only for the radial-centre gesture: R10
/// (use / wear the dragged item on the focused body) must not run against a
/// remote render clone, whose items are display proxies.
///
/// While a proxy drag is in progress the release is CONSUMED here and reported:
/// the intent for this gesture arrives with the item-interaction stage, and
/// leaving it "not consumed" would let the native world fallbacks run and
/// produce a drop or a take-out the player never asked for.
/// </summary>
[HarmonyPatch(typeof(PlayerCamera), "TryPerformRadialAction")]
internal static class PlayerCameraTryPerformRadialActionPatch
{
	private static bool Prefix(PlayerCamera __instance, ref bool __result)
	{
		if (!RemoteBackpackView.IsOpen)
		{
			return true;
		}

		if (RemoteDragProxyQuery.IsProxy(__instance.dragItem))
		{
			PatchBridge.Impl?.ReportRemoteGestureNotCarried("use or wear on the radial centre through the remote view");
			__result = true;
			return false;
		}

		__result = false;
		return false;
	}
}
