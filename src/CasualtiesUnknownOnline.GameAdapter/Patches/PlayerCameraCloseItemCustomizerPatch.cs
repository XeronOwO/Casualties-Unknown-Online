using HarmonyLib;

namespace CasualtiesUnknownOnline.GameAdapter.Patches;

/// <summary>
/// The native customizer writes the item's label, favourite bit and colour directly
/// (PlayerCamera.cs:841, :851-854, :865). Closing it is the player's "I am done" event
/// (PlayerCamera.cs:1004-1006), and the character snapshot is the record a later save
/// restores. Without this report the item's values travel only on the next 1 Hz tick,
/// while a save or a menu-return cut can run first and keep the pre-edit copy.
/// </summary>
[HarmonyPatch(typeof(PlayerCamera), nameof(PlayerCamera.CloseItemCustomizer))]
internal static class PlayerCameraCloseItemCustomizerPatch
{
	private static void Postfix() => PatchBridge.Impl?.OnInventoryChanged();
}
