using CasualtiesUnknownOnline.Runtime.Protocol;
using HarmonyLib;

namespace CasualtiesUnknownOnline.GameAdapter.Patches;

/// <summary>
/// Coil → CoilShocked (repeatable — the 30 s cooldown is the game's own gate):
/// Shock() ran (CoilScript.cs — zap + light + shake; the electric damage hits
/// the triggering side's limb). The event replays the visible state.
/// </summary>
[HarmonyPatch(typeof(CoilScript), "Shock")]
internal static class TrapCoilPatch
{
	private static void Postfix(CoilScript __instance) =>
		PatchBridge.Impl?.OnTrapTriggered(EntityEventKind.CoilShocked, __instance.transform.position, 0);
}
