using CasualtiesUnknownOnline.Runtime.Session.PlayerInteraction;
using HarmonyLib;

namespace CasualtiesUnknownOnline.GameAdapter.Patches;

/// <summary>
/// R10's radial-centre branch, which the release window no longer suppresses: the
/// game's own <c>TryPerformRadialAction</c> runs, and the <c>WearWearable</c> /
/// <c>UseItem</c> calls it makes are captured like every other mutation of the drag
/// window (<see cref="RemoteDragMutationPatches.RemoteDragBodyWearPatch"/>,
/// <see cref="RemoteDragMutationPatches.RemoteDragBodyUseItemPatch"/>).
///
/// This probe covers the one native outcome that consumes the release and runs NO
/// call: an item that is neither wearable nor usable dropped inside the circle
/// (<c>PlayerCamera.cs:1638-1648</c>). Without it the window would log that release
/// as an unclassified gesture CUO has never seen. The verdict is the native method's
/// own answer — CUO never re-derives the branch's geometry.
/// </summary>
[HarmonyPatch(typeof(PlayerCamera), "TryPerformRadialAction")]
internal static class PlayerCameraRadialActionProbePatch
{
	private static void Postfix(bool __result)
	{
		if (__result)
		{
			RemoteDragIntentWindow.Current.NoteClassifiedNoOp(RemoteDragNoOp.RadialCentreWithoutAnAction);
		}
	}
}
