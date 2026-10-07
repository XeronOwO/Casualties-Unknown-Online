using HarmonyLib;

namespace CasualtiesUnknownOnline.GameAdapter.Patches;

/// <summary>
/// The native injection call, diverted for a cross-player session. The operator's
/// client runs the item's own <c>ItemInfo.useLimbAction</c> (so the game decides
/// the syringe minigame, the one-shot dose and the ml rate itself, and CUO keeps
/// no dose table); the delegate that ends up here is that same native path, and
/// the ml it computed is the dose the host commits.
/// <para>
/// When a remote session owns this container, the call is SWALLOWED: the ml goes
/// to the session, the local drain and the local body effect do not happen (the
/// host owns consumption and the patient's client owns the effect), and the
/// displayed body copy is never mutated. Every other injection — the player's own
/// limb, an unrelated native flow — falls through to the untouched original.
/// </para>
/// </summary>
internal static class RemoteInjectionPatches
{
	[HarmonyPatch(typeof(WaterContainerItem), "Inject")]
	internal static class RemoteInjectionDivertPatch
	{
		private static bool Prefix(WaterContainerItem __instance, Limb limb, float amount) =>
			PatchBridge.Impl?.TryDivertRemoteInjection(__instance, limb, amount) != true;
	}
}
