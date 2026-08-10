using CasualtiesUnknownOnline.Runtime.Protocol;
using HarmonyLib;

namespace CasualtiesUnknownOnline.GameAdapter.Patches;

/// <summary>
/// Geyser → GeyserActivated (repeatable, 10 s cooldown): the eruption's
/// Activate() ran (GeyserScript.cs — the 4.5 s liquid spout). The liquid type
/// is NOT part of the event: it is bound at generation time by the host
/// (GeyserStateSnapshot, #128 — GeyserScript.Start rolls it from the PUBLIC
/// random stream, per-side, so the host's roll is the authority). A duplicate
/// is naturally throttled by the game's own cooldown gate.
/// </summary>
[HarmonyPatch(typeof(GeyserScript), "Activate")]
internal static class TrapGeyserPatch
{
	private static void Postfix(GeyserScript __instance) =>
		PatchBridge.Impl?.OnTrapTriggered(EntityEventKind.GeyserActivated, __instance.transform.position, 0);
}
