using HarmonyLib;

namespace CasualtiesUnknownOnline.GameAdapter.Patches;

/// <summary>
/// The remote backpack view is read-only: picking an item out of the focused
/// remote clone's radial buttons must not start a drag. Cross-player take is a
/// separate host-authoritative path; the native view is presentation only.
/// </summary>
[HarmonyPatch(typeof(PlayerCamera), "TryPickupFromUI")]
internal static class PlayerCameraTryPickupFromUIPatch
{
	private static bool Prefix() => !RemoteBackpackView.IsOpen;
}
