using CasualtiesUnknownOnline.Runtime.Protocol;
using HarmonyLib;

namespace CasualtiesUnknownOnline.GameAdapter.Patches;

/// <summary>
/// Geyser → GeyserActivated (repeatable, 10 s cooldown): the eruption's
/// Activate() ran (GeyserScript.cs — the 4.5 s liquid spout). The event
/// carries the liquidType (Extra) so the peers' spouts match; their local
/// TryRumble re-runs the rumble → activate sequence. A duplicate is naturally
/// throttled by the game's own cooldown gate.
/// </summary>
[HarmonyPatch(typeof(GeyserScript), "Activate")]
internal static class TrapGeyserPatch
{
	private static void Postfix(GeyserScript __instance) =>
		PatchBridge.Impl?.OnTrapTriggered(EntityEventKind.GeyserActivated, __instance.transform.position,
			Traverse.Create(__instance).Field("liquidType").GetValue<byte>()); // byte, not int — a GetValue<int> cast throws InvalidCastException and kills the report
}
