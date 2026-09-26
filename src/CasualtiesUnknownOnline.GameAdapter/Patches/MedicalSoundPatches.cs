using System;
using HarmonyLib;

namespace CasualtiesUnknownOnline.GameAdapter.Patches;

/// <summary>
/// Limb-treatment sound capture. <c>PlayerCamera.ApplyWoundItem</c>
/// (PlayerCamera.cs:739) is the choke point every local limb action enters: the
/// item's own <c>useLimbAction</c> delegate (:754) and the container's
/// <c>WaterContainerItem.ApplyToLimb</c> (:760, whose <c>onHealthUse</c>
/// delegates are where the liquid clips play) both run inside it, and every
/// medical clip the family plays is played by one of those two branches at the
/// treated limb's position — which may be ANOTHER player's body.
/// <para>
/// The scope opens only for a plain local action on this client's camera: the
/// remote medical view blocks the native call
/// (<c>RemoteMedicalBlockApplyWoundItemPatch</c>) and a drag-release window
/// captures the intent instead of applying it, so neither path can report as
/// the local player's action. The native calls pass no follow transform, so the
/// report stays position-based and the peers replay the clip at the limb the
/// treatment touched.
/// </para>
/// </summary>
internal static class MedicalSoundPatches
{
	[HarmonyPatch(typeof(PlayerCamera), "ApplyWoundItem")]
	internal static class ApplyWoundItemSoundPatch
	{
		private static void Prefix(PlayerCamera __instance, out IDisposable? __state) =>
			__state = CaptureScopeGuard.IsLocalAction() && __instance == PlayerCamera.main // Unity objects — ==
				? CallContext.Enter(CallContext.Origin.CharacterMedicalUse)
				: null;

		private static void Postfix(IDisposable? __state) => __state?.Dispose();
	}
}
