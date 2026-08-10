namespace CasualtiesUnknownOnline.GameAdapter.Patches;

/// <summary>
/// Dripping crystal (guest session): the drip writes the fluid grid — the
/// HOST's grid is the authority (the stream carries its drips). CrystalDripping
/// is INTERNAL to the game assembly, so this Prefix is installed dynamically
/// beside CrystalFragile/CrystalElectric (the InstallDynamicPatches pattern);
/// only its Update is intercepted — the Start visual (the 0.7 x scale) runs
/// untouched. Host/solo: original behaviour.
/// </summary>
internal static class CrystalDrippingPatch
{
	internal static bool Prefix()
	{
		if (PatchBridge.Impl is { } bridge && bridge.IsSessionActive && !bridge.IsHostMode)
		{
			return false; // guest — the host's grid is the fluid authority
		}

		return true;
	}
}
