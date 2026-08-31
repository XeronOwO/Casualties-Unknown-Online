using CasualtiesUnknownOnline.GameAdapter.Character;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using HarmonyLib;

namespace CasualtiesUnknownOnline.GameAdapter.Patches;

/// <summary>
/// Remote render clones must show the owner's weapon recoil when they fire.
/// The clone's gun direction is already synced: Body.HandleVisuals computes
/// <c>gunangle</c> from the peer's <c>targetLookPos</c> (Body.cs:3271) and
/// SessionStatePump writes that aim into every render clone. What was missing
/// is the one-shot kick <c>GunScript.Fire</c> applies to the OWNER's local
/// arms animator (<c>knockBack * 8</c>, GunScript.cs:221). This postfix
/// reports that fire as a <see cref="CharacterSoundKind.GunFire"/> event (the
/// exact fire-sound clip name + volume/2D mode + recoil degrees), and
/// CharacterSoundSync replays it on the owner's clone. The clone's
/// Body.HandleVisuals then lerps the extra gunangle back to the synced aim,
/// which is exactly what recoil looks like.
/// </summary>
[HarmonyPatch(typeof(GunScript), "Fire", [typeof(bool)])]
internal static class GunFirePatch
{
	private static void Postfix(GunScript __instance)
	{
		if (__instance.GetComponentInParent<RemoteBodyDriver>() != null) // Unity object — ==
		{
			// A render clone never fires; if a stray path calls Fire on a
			// clone's GunScript it must not report (and would not simulate
			// the local player's body either).
			return;
		}

		var clip = __instance.fireSound != null ? __instance.fireSound.name : ""; // Unity object — ==
		if (string.IsNullOrEmpty(clip))
		{
			return;
		}

		PatchBridge.Impl?.OnCharacterSound(
			CharacterSoundKind.GunFire,
			clip,
			__instance.transform.position,
			volume: 1f,
			followOwner: false,
			twoDimensional: true,
			recoilDegrees: __instance.knockBack * 8f);
	}
}
