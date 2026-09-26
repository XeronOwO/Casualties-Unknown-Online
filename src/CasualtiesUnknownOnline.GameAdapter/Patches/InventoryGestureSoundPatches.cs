using System;
using HarmonyLib;

namespace CasualtiesUnknownOnline.GameAdapter.Patches;

/// <summary>
/// The inventory gesture whose native call carries no scope of its own:
/// <c>Body.CombineLiquids</c> (Body.cs:1231) is reached from the transfer UI's
/// finish (LiquidTransfer.cs:38) and plays <c>"waterpour"</c>.
/// <para>
/// The other two gestures are classified from scopes CUO already opens for its
/// own reasons — <c>InternalReorder</c> around SwitchHands / SwapSlots and
/// <c>Craft</c> around CombineItems. They deliberately get no second, nested
/// scope here: nesting a capture origin inside them would hide the origin from
/// the guards that read it (the drop/pickup hooks stay silent under those two
/// origins, and the item-end destroys stay silent under <c>Craft</c>).
/// </para>
/// </summary>
internal static class InventoryGestureSoundPatches
{
	[HarmonyPatch(typeof(Body), "CombineLiquids")]
	internal static class CombineLiquidsSoundPatch
	{
		// Same eligibility rule as every other capture scope: a plain local action
		// on THIS client's own body. The sole caller today (LiquidTransfer.Finish →
		// PlayerCamera.main.body) always satisfies both, so the guard is what keeps
		// the "reported only for this client's own action" claim true rather than
		// merely unreachable-by-accident.
		private static void Prefix(Body __instance, out IDisposable? __state) =>
			__state = CaptureScopeGuard.IsLocalAction() && CaptureScopeGuard.IsLocalPlayerBody(__instance)
				? CallContext.Enter(CallContext.Origin.CharacterInventoryGesture)
				: null;

		private static void Postfix(IDisposable? __state) => __state?.Dispose();
	}
}
