using HarmonyLib;

namespace CasualtiesUnknownOnline.GameAdapter.Patches;

/// <summary>
/// The native drink call, diverted while this client measures a cross-player
/// drink. The operator's client runs the item's own <c>ItemInfo.useAction</c> (so
/// the game decides the per-use ml itself and CUO keeps no dose table); the
/// delegate that ends up here is that same native path, and the ml it computed is
/// the dose the host commits.
/// <para>
/// When a measurement owns this container, the call is SWALLOWED: the ml goes to
/// the request, and neither the local drain nor the liquids' own <c>onDrink</c>
/// bodies run here (the host owns consumption and the patient's client owns the
/// effect). Every other drink — the player's own, an unrelated native flow — falls
/// through to the untouched original.
/// </para>
/// </summary>
internal static class RemoteDrinkPatches
{
	[HarmonyPatch(typeof(WaterContainerItem), "Drink")]
	internal static class RemoteDrinkDivertPatch
	{
		private static bool Prefix(WaterContainerItem __instance, float amount) =>
			PatchBridge.Impl?.TryDivertRemoteDrink(__instance, amount) != true;
	}
}
