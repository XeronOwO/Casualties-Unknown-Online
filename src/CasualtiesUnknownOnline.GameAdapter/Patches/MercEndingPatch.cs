using System.Collections;
using CasualtiesUnknownOnline.GameState.Domains.World;
using CasualtiesUnknownOnline.Runtime.Session.World;
using HarmonyLib;

namespace CasualtiesUnknownOnline.GameAdapter.Patches;

/// <summary>
/// The run's ending is the host's, not each client's (ticket <c>backlog/done/1.0-endings-authority.md</c>).
///
/// <para>
/// The release ends a run at the bottom layer with one of two branches — the merchant's ending when the
/// host carries the scarf, else the surface breach (<c>WorldGeneration.cs:1147-1151</c> starts the
/// enumerator the game itself chose). A member's client runs the same exit check from its own body, so
/// without this patch two clients can play different endings.
/// </para>
///
/// <para>
/// The host records the branch it resolved (<see cref="RunEndingTracker.Record"/>); the run projection
/// stamps it into the run row the advance carries, and every client's projection applies it back. A
/// member whose local branch differs from the host's returns the host's enumerator instead of its own —
/// the same native method, so the cutscene is the game's, not a reimplementation.
/// </para>
/// </summary>
[HarmonyPatch(typeof(WorldGeneration), "MercEnding")]
internal static class MercEndingPatch
{
	private static bool Prefix(WorldGeneration __instance, ref IEnumerator __result) =>
		EndingBranch.Redirect(__instance, RunEnding.Merc, ref __result);
}
