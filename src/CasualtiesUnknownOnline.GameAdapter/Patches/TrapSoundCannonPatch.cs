using CasualtiesUnknownOnline.Runtime.Protocol;
using HarmonyLib;

namespace CasualtiesUnknownOnline.GameAdapter.Patches;

/// <summary>
/// Sound cannon → SoundCannonFired (one-shot): the 5 s charge finished and
/// spent flipped (SoundCannon.cs — the deafening blast hits the LOCAL player's
/// UI on the triggering side; the peers' copies must also stop charging and
/// mark spent). Pure observation.
/// </summary>
[HarmonyPatch(typeof(SoundCannon), "Update")]
internal static class TrapSoundCannonPatch
{
	private static void Prefix(SoundCannon __instance, out bool __state) =>
		__state = Traverse.Create(__instance).Field("spent").GetValue<bool>();

	private static void Postfix(SoundCannon __instance, bool __state)
	{
		if (__state || !Traverse.Create(__instance).Field("spent").GetValue<bool>())
		{
			return; // not the false → true transition
		}

		PatchBridge.Impl?.OnTrapTriggered(EntityEventKind.SoundCannonFired, __instance.transform.position, 0);
	}
}
