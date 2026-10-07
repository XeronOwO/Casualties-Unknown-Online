using HarmonyLib;

namespace CasualtiesUnknownOnline.GameAdapter.Patches;

/// <summary>
/// The native topical call, diverted while this client measures a cross-player
/// topical use. The operator's client runs the item's own
/// <c>ItemInfo.useLimbAction</c> (so the game decides the per-use ml itself and
/// CUO keeps no dose table); the delegate that ends up here is that same native
/// path, and the ml it computed is the dose the host commits.
/// <para>
/// When a measurement owns this container and limb, the call is SWALLOWED: the
/// ml goes to the request, the local drain and the local body effect do not
/// happen (the host owns consumption and the patient's client owns the effect),
/// and the displayed body copy is never mutated. Every other application — the
/// player's own limb, an unrelated native flow — falls through to the untouched
/// original.
/// </para>
/// </summary>
internal static class RemoteTopicalPatches
{
	[HarmonyPatch(typeof(WaterContainerItem), "ApplyToLimb")]
	internal static class RemoteTopicalDivertPatch
	{
		private static bool Prefix(WaterContainerItem __instance, Limb limb, float amount) =>
			PatchBridge.Impl?.TryDivertRemoteTopicalApply(__instance, limb, amount) != true;
	}
}
