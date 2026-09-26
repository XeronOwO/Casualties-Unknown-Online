using System;
using HarmonyLib;

namespace CasualtiesUnknownOnline.GameAdapter.Patches;

/// <summary>
/// Bandage-minigame sound capture. The bandage family's clip is not played by
/// the limb action the treatment enters: that action only STARTS the native
/// <c>BandageMinigame</c>, and the wrap that plays <c>"bandage"</c> (3D, at the
/// carried item — <c>BandageMinigame.cs:112</c>) happens frames later, after
/// <c>PlayerCamera.ApplyWoundItem</c> returned and its capture scope closed.
/// Only the acting client heard it, on BOTH paths: the local limb treatment and
/// the remote one, which CUO drives through this same native minigame
/// (<c>RemoteOtherMedicalOperationHandler</c>).
/// <para>
/// The scope is therefore entered per PHYSICS STEP — the shape the body one-shot
/// coroutines already use for their own delayed clips — and never kept open
/// across frames. It opens only for a plain local action on this client, the
/// same guard every other capture scope in the family carries.
/// </para>
/// </summary>
internal static class BandageMinigameSoundPatches
{
	[HarmonyPatch(typeof(BandageMinigame), "PhysicsUpdate")]
	internal static class PhysicsUpdateSoundPatch
	{
		private static void Prefix(out IDisposable? __state) =>
			__state = CaptureScopeGuard.IsLocalAction()
				? CallContext.Enter(CallContext.Origin.CharacterMedicalUse)
				: null;

		private static void Postfix(IDisposable? __state) => __state?.Dispose();
	}
}
