using CasualtiesUnknownOnline.Runtime.Protocol;
using HarmonyLib;

namespace CasualtiesUnknownOnline.GameAdapter.Patches;

/// <summary>
/// Cave-tick nest → CaveTicksSpawned (one-shot): a limb/body entered and the
/// nest started (CaveTickSpawner.cs:18-38 — the 16 spiders spawn over 1.6 s
/// and ride the EntitySpawned channel + EnemySyncCoordinator runtime binding;
/// the nest itself stops its particles and dies). The event consumes the nest
/// on the peers.
/// </summary>
[HarmonyPatch(typeof(CaveTickSpawner), "OnTriggerEnter2D")]
internal static class TrapCaveTickSpawnerPatch
{
	private static void Prefix(CaveTickSpawner __instance, out bool __state) =>
		__state = Traverse.Create(__instance).Field("started").GetValue<bool>();

	private static void Postfix(CaveTickSpawner __instance, bool __state)
	{
		if (__state || !Traverse.Create(__instance).Field("started").GetValue<bool>())
		{
			return; // not the false → true transition
		}

		PatchBridge.Impl?.OnTrapTriggered(EntityEventKind.CaveTicksSpawned, __instance.transform.position, 0);
	}
}
