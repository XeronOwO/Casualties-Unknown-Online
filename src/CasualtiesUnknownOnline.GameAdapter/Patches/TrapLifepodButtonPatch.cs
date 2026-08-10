using CasualtiesUnknownOnline.Runtime.Protocol;
using HarmonyLib;

namespace CasualtiesUnknownOnline.GameAdapter.Patches;

/// <summary>
/// Life-pod buttons → lifepod events: the heat button (type 0, ToggleHeatState
/// — heatState cycles 0/1/2) and the shower button (type 1, ActivateShower).
/// Pure observation: the game's own toggle ran; the event makes the peers'
/// copies toggle to the same state. Position-keyed on the CONTROLLER (both
/// buttons share it — that is the entity whose state diverges).
/// </summary>
[HarmonyPatch(typeof(LifepodButton), "OnUse")]
internal static class TrapLifepodButtonPatch
{
	private static void Postfix(LifepodButton __instance)
	{
		if (__instance.controller == null) // Unity object — ==
		{
			return;
		}

		var kind = __instance.type == 0 ? EntityEventKind.LifepodHeatChanged : EntityEventKind.LifepodShowerActivated;
		var pos = __instance.controller.transform.position;
		var extra = kind == EntityEventKind.LifepodHeatChanged ? (byte)__instance.controller.heatState : (byte)0;
		PatchBridge.Impl?.OnTrapTriggered(kind, pos, extra);
	}
}
