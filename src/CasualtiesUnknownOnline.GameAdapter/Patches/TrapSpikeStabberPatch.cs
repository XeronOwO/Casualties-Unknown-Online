using CasualtiesUnknownOnline.Runtime.Protocol;
using HarmonyLib;

namespace CasualtiesUnknownOnline.GameAdapter.Patches;

/// <summary>
/// Spikestabber → SpikeStabbed: the one-shot Stab() ran (SpikeStabberScript.cs:
/// 49-55 — activated + the SpikeStab anim; the CheckStab frame callback then
/// hurts whoever stands above). Pure observation; the event makes the peers'
/// copies stab too (their local real bodies get the CheckStab treatment).
/// </summary>
[HarmonyPatch(typeof(SpikeStabberScript), "Stab")]
internal static class TrapSpikeStabberPatch
{
	private static void Postfix(SpikeStabberScript __instance) =>
		PatchBridge.Impl?.OnTrapTriggered(EntityEventKind.SpikeStabbed, __instance.transform.position, 0);
}
