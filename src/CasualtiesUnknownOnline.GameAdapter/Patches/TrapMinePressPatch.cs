using CasualtiesUnknownOnline.Runtime.Protocol;
using HarmonyLib;

namespace CasualtiesUnknownOnline.GameAdapter.Patches;

/// <summary>
/// Mine pressed → MinePressed (transient one-way edge): OnCollisionEnter2D sets
/// `pressed = true` and plays the native 0.8 s press visual (MineScript.cs:44-51 —
/// mine sound + pressedSprite). Pure observation — the native method runs
/// untouched; the prefix captures the latch, the postfix detects the rise and
/// reports the event at its TRUE start, before the 0.8 s MineExploded fires.
/// The receiving sides replay only the visual (never the physical `pressed`
/// latch), so the explosion itself stays exclusively on the MineExploded event.
/// </summary>
[HarmonyPatch(typeof(MineScript), "OnCollisionEnter2D")]
internal static class TrapMinePressPatch
{
	private static void Prefix(MineScript __instance, out bool __state) =>
		__state = Traverse.Create(__instance).Field("pressed").GetValue<bool>();

	private static void Postfix(MineScript __instance, bool __state)
	{
		var now = Traverse.Create(__instance).Field("pressed").GetValue<bool>();
		if (!now || __state)
		{
			return; // not the false → true pressed transition
		}

		PatchBridge.Impl?.OnTrapTriggered(EntityEventKind.MinePressed, __instance.transform.position, 0);
	}
}
