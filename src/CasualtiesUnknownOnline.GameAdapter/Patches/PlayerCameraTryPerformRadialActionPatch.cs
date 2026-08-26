using HarmonyLib;

namespace CasualtiesUnknownOnline.GameAdapter.Patches;

/// <summary>
/// The remote backpack view is read-only: the radial-centre actions (use,
/// wear, drop on the focused body) must not run against a remote render clone.
/// The clone's items are display proxies, not the owner's authoritative
/// inventory; mutating them would be a local-only illusion.
/// </summary>
[HarmonyPatch(typeof(PlayerCamera), "TryPerformRadialAction")]
internal static class PlayerCameraTryPerformRadialActionPatch
{
	private static bool Prefix(ref bool __result)
	{
		if (RemoteBackpackView.IsOpen)
		{
			__result = false;
			return false;
		}

		return true;
	}
}
