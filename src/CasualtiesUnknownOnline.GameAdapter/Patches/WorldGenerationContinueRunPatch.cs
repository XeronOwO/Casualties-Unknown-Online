using HarmonyLib;

namespace CasualtiesUnknownOnline.GameAdapter.Patches;

/// <summary>
/// The end-of-layer choice's marker. The native panel's "Continue" is
/// <c>WorldGeneration.ContinueRun</c> — a public entry with no caller anywhere in
/// the assembly, so the scene's own panel is its only producer — and it is the
/// only producer that reaches a descent from a panel click: the drill pod and the
/// debug console start <c>RegenerateWorld</c> themselves and carry their own
/// semantics (two layers, the pod's own arrival effects), which this change
/// deliberately leaves alone. This patch therefore marks the duration of the
/// entry's own body so the regeneration sink can tell those producers apart
/// (<c>WorldGenerationUpdatePatch.InUpdate</c> is the same pattern for the same
/// reason), and clears the mark afterwards — including the case where the entry's
/// own guard refused and no regeneration follows at all.
/// </summary>
[HarmonyPatch(typeof(WorldGeneration), "ContinueRun")]
internal static class WorldGenerationContinueRunPatch
{
	/// <summary>True while the native end-of-layer entry's own body runs — the window in which a regeneration comes from the panel's click and not from the pod or the console.</summary>
	internal static bool InContinueRun;

	/// <summary>
	/// Read the mark and clear it in the same call. The sink is the entry's last step, so the window ends
	/// there: a body that threw between the mark and the regeneration cannot leak the mark into the pod's or
	/// the console's next descent. The postfix below still clears it for the case where the entry's own guard
	/// refused and no regeneration followed at all.
	/// </summary>
	internal static bool ConsumeEntryMarker()
	{
		var marked = InContinueRun;
		InContinueRun = false;
		return marked;
	}

	private static void Prefix() => InContinueRun = true;

	private static void Postfix() => InContinueRun = false;
}
