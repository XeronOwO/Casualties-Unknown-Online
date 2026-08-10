using HarmonyLib;

namespace CasualtiesUnknownOnline.GameAdapter.Patches;

/// <summary>
/// Oil pipe (guest session): the oil production writes the fluid grid — the
/// HOST's grid is the authority (its own pipe writes; the stream carries the
/// result to the guests). Host/solo: original behaviour.
/// </summary>
[HarmonyPatch(typeof(OilPipeScript), "Update")]
internal static class OilPipePatch
{
	private static bool Prefix()
	{
		if (PatchBridge.Impl is { } bridge && bridge.IsSessionActive && !bridge.IsHostMode)
		{
			return false; // guest — the host's grid is the fluid authority
		}

		return true;
	}
}
