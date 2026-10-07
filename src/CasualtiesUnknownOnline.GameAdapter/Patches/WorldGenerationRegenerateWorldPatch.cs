using System.Collections;
using CasualtiesUnknownOnline.GameAdapter.World;
using HarmonyLib;

namespace CasualtiesUnknownOnline.GameAdapter.Patches;

/// <summary>
/// The end-of-layer descent's sink. Every layer advance in the game runs through
/// <c>WorldGeneration.RegenerateWorld(bool twice)</c> — the panel's entry
/// (<c>WorldGeneration.cs:1018</c>), the drill pod (<c>DrillPod.cs:29</c>, by
/// name and with <c>twice</c>) and the console's <c>skiplayer</c>
/// (<c>ConsoleScript.cs:721</c>) — and the ones that matter here differ in a way
/// only the caller knows: a panel click means "this member judged the layer
/// finished", which a session must take from the HOST, while the pod and the
/// console carry semantics of their own (two layers, the pod's arrival effects)
/// that this change does not touch.
/// <para>
/// So this is a prefix on the sink, not on the entry: the game's own entry body
/// still runs unchanged — its guard clauses, the panel close, the walk release and
/// the deepest-layer record are all native — and by the time this prefix runs, the
/// marker tells whether the call came from that body
/// (<see cref="WorldGenerationContinueRunPatch.InContinueRun"/>). When it did, and
/// the port then says this client is a guest in a live session, the returned
/// enumerator is REPLACED by an empty one: a valid object (the entry hands it to
/// <c>StartCoroutine</c>, so it may not be null) that starts no regeneration,
/// because the layer comes from the host's baseline. Every other caller returns
/// true and keeps the game's own path.
/// </para>
/// </summary>
[HarmonyPatch(typeof(WorldGeneration), "RegenerateWorld")]
internal static class WorldGenerationRegenerateWorldPatch
{
	private static bool Prefix(ref IEnumerator __result)
	{
		// The mark is consumed here (not merely read): this is the entry's last step, so the window ends with
		// this call even if the entry's body threw on its way here.
		var fromPanelEntry = WorldGenerationContinueRunPatch.ConsumeEntryMarker();

		// The port is asked ONLY for the panel's own click: it reports the choice as its side effect, and the
		// pod's and the console's own descents keep the game's semantics (their own tickets).
		var delegated = fromPanelEntry && (PatchBridge.LayerAdvance?.TryDelegateLocalAdvance() ?? false);
		if (!LayerAdvancePolicy.ShouldSuppressLocalDescent(fromPanelEntry, delegated))
		{
			return true;
		}

		__result = Delegated();
		return false;
	}

	/// <summary>What the member's client runs instead of the descent: nothing (the choice was reported; the layer comes from the host's baseline).</summary>
	private static IEnumerator Delegated()
	{
		yield break;
	}
}
