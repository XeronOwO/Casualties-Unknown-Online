using HarmonyLib;

namespace CasualtiesUnknownOnline.GameAdapter.Patches;

/// <summary>
/// While the native remote-backpack view is open, <c>PlayerCamera.HandleTradeMenu</c>
/// must not re-anchor the radial menu to the local body. The remote view owns
/// the radial anchor for this frame (see
/// <see cref="PlayerCameraHandleWhileDraggingPatch"/>); allowing the original
/// trade-menu line to run would move the radial away from the focused remote
/// clone even though the buttons were already routed to that clone.
/// </summary>
[HarmonyPatch(typeof(PlayerCamera), "HandleTradeMenu")]
internal static class PlayerCameraHandleTradeMenuPatch
{
	private static bool Prefix(PlayerCamera __instance) =>
		!RemoteBackpackView.IsOpen || __instance.tradeMenu.activeSelf;
}
