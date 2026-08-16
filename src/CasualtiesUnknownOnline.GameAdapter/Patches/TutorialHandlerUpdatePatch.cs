using System;
using HarmonyLib;

namespace CasualtiesUnknownOnline.GameAdapter.Patches;

/// <summary>
/// Opens the TutorialClawSpawn scope around TutorialHandler.Update — the only
/// place the tutorial claw creates course objects (TutorialHandler.cs:255-271).
/// Utils.Create calls inside that window are marked as per-player tutorial
/// props by <see cref="UtilsCreateTutorialPatch"/>; the item/entity domains
/// then leave them out of the shared tables instead of double-reporting both
/// sides' copies (the claw double-give). Prefix/Postfix with __state only —
/// no cross-call business state (AGENTS.md #10); the scope is disposed on
/// exception paths too.
/// </summary>
[HarmonyPatch(typeof(TutorialHandler), "Update")]
internal static class TutorialHandlerUpdatePatch
{
	private static void Prefix(out IDisposable? __state) =>
		__state = CallContext.Enter(CallContext.Origin.TutorialClawSpawn);

	private static void Postfix(IDisposable? __state) => __state?.Dispose();
}
