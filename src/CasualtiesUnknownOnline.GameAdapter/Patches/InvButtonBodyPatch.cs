using HarmonyLib;

namespace CasualtiesUnknownOnline.GameAdapter.Patches;

/// <summary>
/// Native backpack view routing: while the remote backpack view is open, every
/// <see cref="InvButton"/> must read its slots from the focused remote clone
/// instead of the local <c>PlayerCamera.main.body</c>. This is the same seam
/// KrokMP used to make the native radial inventory show another player's body.
/// Presentation-only; the focused clone is never the authority.
/// </summary>
[HarmonyPatch(typeof(InvButton), "get_body")]
internal static class InvButtonBodyPatch
{
	private static void Postfix(InvButton __instance, ref Body __result)
	{
		if (!RemoteBackpackView.IsOpen)
		{
			RemoteBackpackView.Close();
			return;
		}

		if (RemoteBackpackView.FocusedBody is { } focused && focused != PlayerCamera.main.body) // Unity objects — ==
		{
			__result = focused;
		}
	}
}
