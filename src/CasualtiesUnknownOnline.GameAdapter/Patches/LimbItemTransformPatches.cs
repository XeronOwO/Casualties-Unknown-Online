using CasualtiesUnknownOnline.GameAdapter.Items;
using HarmonyLib;

namespace CasualtiesUnknownOnline.GameAdapter.Patches;

/// <summary>
/// The native LIMB-ITEM transforms, hooked where the game performs them (the
/// adapter's half of the applied-item rule; the reasoning lives in
/// <see cref="LimbItemTransform"/>).
/// <list type="bullet">
/// <item><c>PlayerCamera.ApplyWoundItem</c> — the ONE choke point every local limb
/// action enters (<c>PlayerCamera.cs:739-763</c>: the item's own
/// <c>useLimbAction</c> at :754, a liquid container's <c>ApplyToLimb</c> at :760).
/// The postfix compares the selected limb's component set before and after the
/// call, and only the two component-bearing tool types count: they are the family
/// the game itself defines (only they declare <c>OnDismember</c>/<c>TakeOff</c>), so
/// the ice pack's <c>ChilledLimb</c> branch and every tool that merely spends
/// condition stay outside it.</item>
/// <item><c>SplintLimb.TakeOff</c> / <c>TourniquetScript.TakeOff</c> — the component
/// instantiating the item it revives (<c>SplintLimb.cs:19-26</c>,
/// <c>TourniquetScript.cs:25-35</c>). The prefix resolves which item that is, the
/// <c>Item.Awake</c> hook in <see cref="ItemPatches"/> stamps it, and the postfix
/// reports the move — the same prefix/postfix <c>__state</c> shape the remote
/// medical patches use.</item>
/// </list>
/// <para>
/// A remote limb is deliberately NOT this patch's business: the remote medical view
/// blocks <c>ApplyWoundItem</c> while it is open, a release onto a displayed body is
/// captured by <c>RemoteDragApplyWoundItemPatch</c>, and a cross-player removal is the
/// host's own commit. Those two prefixes return false, and — the fact this file's first
/// cut got wrong — <c>false</c> skips the ORIGINAL and the prefixes after it but NOT the
/// postfixes (<c>WorldTimeSync</c>: "not run, __runOriginal is false, and the postfix
/// still fires"). Both postfixes therefore ask <c>__runOriginal</c> themselves instead of
/// assuming they do not run: an apply that never ran changes no component and reports
/// nothing, and a take-off that never ran drops its armed record instead of reporting a
/// removal that did not happen.
/// </para>
/// </summary>
internal static class LimbItemTransformPatches
{
	[HarmonyPatch(typeof(PlayerCamera), "ApplyWoundItem")]
	internal static class LimbApplyWoundItemPatch
	{
		private static void Prefix(PlayerCamera __instance, out ApplyState __state)
		{
			var limb = __instance.selectedLimb;
			__state = limb == null // Unity object — ==
				? default
				: new ApplyState(
					limb,
					limb.GetComponent<SplintLimb>() != null, // Unity object — ==
					limb.GetComponent<TourniquetScript>() != null); // Unity object — ==
		}

		private static void Postfix(Item item, bool __runOriginal, ApplyState __state)
		{
			if (!__runOriginal || __state.Limb is not { } limb)
			{
				return;
			}

			// Per type, and only a component this call ADDED: one limb legally carries a splint
			// and a tourniquet at once, and a limb that already had a splint did not gain one
			// when its tourniquet was applied.
			var gained = (limb.GetComponent<SplintLimb>() != null && !__state.HadSplint) // Unity object — ==
				|| (limb.GetComponent<TourniquetScript>() != null && !__state.HadTourniquet); // Unity object — ==
			if (gained)
			{
				PatchBridge.Impl?.OnItemAppliedToLimb(item, limb);
			}
		}

		internal readonly record struct ApplyState(Limb Limb, bool HadSplint, bool HadTourniquet);
	}

	[HarmonyPatch(typeof(SplintLimb), "TakeOff")]
	internal static class SplintTakeOffPatch
	{
		private static void Prefix(SplintLimb __instance) =>
			// The component's own template field is what names the item it revives, and it is the
			// same string the removal's applied-row lookup keys on (a splint and a carcass splint
			// are two definitions that share one component type).
			PatchBridge.Impl?.OnLimbComponentTakeOffBegin(__instance.gameObject, __instance.item);

		// The verdict is passed through rather than assumed: a skipped original still leaves this
		// postfix running, and the armed record must be DROPPED instead of reported as a removal.
		private static void Postfix(bool __runOriginal) => PatchBridge.Impl?.OnLimbComponentTakeOffEnd(__runOriginal);
	}

	[HarmonyPatch(typeof(TourniquetScript), "TakeOff")]
	internal static class TourniquetTakeOffPatch
	{
		// The native call hardcodes the template it revives (`Resources.Load("tourniquet")`,
		// TourniquetScript.cs:27), so the definition is a constant here rather than a field read.
		private static void Prefix(TourniquetScript __instance) =>
			PatchBridge.Impl?.OnLimbComponentTakeOffBegin(__instance.gameObject, "tourniquet");

		private static void Postfix(bool __runOriginal) => PatchBridge.Impl?.OnLimbComponentTakeOffEnd(__runOriginal);
	}
}
