using CasualtiesUnknownOnline.Runtime.Protocol;
using HarmonyLib;

namespace CasualtiesUnknownOnline.GameAdapter.Patches;

/// <summary>
/// Life-pod shuttle door → ShuttleDoorOpened event: a body entered the door's
/// trigger (ShuttleStartOpen.cs:44-56 — the local side's real body, the only
/// trigger a clone never provides) and the door starts opening. Pure
/// observation: the game's own activation runs untouched; the event makes the
/// peer's copy activate too (the entity's own Update then drives the door
/// animation on both sides from the same start moment).
/// </summary>
[HarmonyPatch(typeof(ShuttleStartOpen), "OnTriggerEnter2D")]
internal static class TrapShuttleDoorPatch
{
	private static void Prefix(ShuttleStartOpen __instance, out bool __state) =>
		__state = Traverse.Create(__instance).Field("activated").GetValue<bool>();

	private static void Postfix(ShuttleStartOpen __instance, bool __state)
	{
		if (__state || !Traverse.Create(__instance).Field("activated").GetValue<bool>())
		{
			return; // not the closed → activated transition
		}

		PatchBridge.Impl?.OnTrapTriggered(EntityEventKind.ShuttleDoorOpened, __instance.transform.position, 0);
	}
}
