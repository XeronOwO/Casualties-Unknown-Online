using CasualtiesUnknownOnline.Runtime.Protocol;
using HarmonyLib;

namespace CasualtiesUnknownOnline.GameAdapter.Patches;

/// <summary>
/// Stalactite → StalactiteDropped (one-shot): Drop() ran (StalactiteDropper.cs
/// — the ceiling spike falls; its DamagingCrate hurts whoever it lands on).
/// Pure observation; the event makes the peers' copies drop too (the falling
/// spike and its impact are visible/damaging on every side).
/// </summary>
[HarmonyPatch(typeof(StalactiteDropper), "Drop")]
internal static class TrapStalactitePatch
{
	private static void Postfix(StalactiteDropper __instance) =>
		PatchBridge.Impl?.OnTrapTriggered(EntityEventKind.StalactiteDropped, __instance.transform.position, 0);
}
