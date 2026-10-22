using HarmonyLib;

namespace CasualtiesUnknownOnline.GameAdapter.Patches;

/// <summary>
/// Remembers the access state a display-proxy drag started under. The double-Tab
/// transfer closes the remote view BEFORE it releases the proxy onto the local
/// backpack, so by release time the view is gone and its read-only flag with it; the
/// flag captured here is what the dispatcher refuses the intent on. A local item's
/// drag clears it, so a later gesture can never inherit a stale refusal.
/// </summary>
[HarmonyPatch(typeof(PlayerCamera), "HandleStartDragging")]
internal static class ReadOnlyBackpackDragStartPatch
{
	private static void Postfix(PlayerCamera __instance)
	{
		if (RemoteDragProxyQuery.IsProxy(__instance.dragItem))
		{
			RemoteBackpackView.BeginProxyDrag();
			return;
		}

		RemoteBackpackView.EndProxyDrag();
	}
}
