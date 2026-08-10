using CasualtiesUnknownOnline.Runtime.Protocol;
using HarmonyLib;

namespace CasualtiesUnknownOnline.GameAdapter.Patches;

/// <summary>
/// Mine triggers → MineExploded event. Both paths are pure observation (no
/// interception — the game's own explosion runs untouched):
/// - Update: the pressed → 0.8 s → exploded transition (MineScript.cs:31-38).
///   The exploded flag is private; the prefix captures it, the postfix
///   detects the rise and reports the event.
/// - OnDestroy: the CHAIN path (MineScript.cs:16-23 — a mine destroyed with
///   health below 0.5 that never exploded explodes on destruction). The game's
///   own explosion runs first; this only reports it, so a blast wave rolling
///   through a minefield syncs every chain mine. A remote-death destroy
///   carries exploded = true (the applier/replay sets it before killing), so
///   the game skips its explosion and this hook sees the flag already set —
///   no report, no double event.
/// </summary>
internal static class TrapMineExplosionPatch
{
	[HarmonyPatch(typeof(MineScript), "Update")]
	internal static class UpdatePatch
	{
		private static void Prefix(MineScript __instance, out bool __state) =>
			__state = Traverse.Create(__instance).Field("exploded").GetValue<bool>();

		private static void Postfix(MineScript __instance, bool __state)
		{
			if (__state || !Traverse.Create(__instance).Field("exploded").GetValue<bool>())
			{
				return; // not the pressed → exploded transition
			}

			PatchBridge.Impl?.OnTrapTriggered(EntityEventKind.MineExploded, __instance.transform.position, 0);
		}
	}

	[HarmonyPatch(typeof(MineScript), "OnDestroy")]
	internal static class DestroyPatch
	{
		private static void Postfix(MineScript __instance)
		{
			var build = __instance.build;
			// == null: Unity object. The chain condition is the game's own
			// (MineScript.cs:16 — health below 0.5 and never exploded).
			if (build == null || build.health >= 0.5f || Traverse.Create(__instance).Field("exploded").GetValue<bool>())
			{
				return;
			}

			PatchBridge.Impl?.OnTrapTriggered(EntityEventKind.MineExploded, __instance.transform.position, 0);
		}
	}
}
