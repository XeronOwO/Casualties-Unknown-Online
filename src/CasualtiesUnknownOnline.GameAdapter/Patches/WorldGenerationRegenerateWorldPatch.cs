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
	private static bool Prefix(ref IEnumerator __result, object[] __args)
	{
		// The mark is consumed here (not merely read): this is the entry's last step, so the window ends with
		// this call even if the entry's body threw on its way here.
		var fromPanelEntry = WorldGenerationContinueRunPatch.ConsumeEntryMarker();

		// The drill pod's own descent is the other call this session owns: it starts the coroutine BY NAME,
		// right after setting `doPod`, so the flag names the producer here (the console's `skiplayer` sets
		// neither). The two-layer step it carries rides into the request; the pod's arrival effects stay on
		// the rider's own client through the same `doPod` flag its own regeneration reads.
		var world = WorldGeneration.world;
		var fromPod = world != null && world.doPod; // Unity object — ==
		var twice = __args is { Length: > 0 } && __args[0] is bool flag && flag;

		// One delegation for both session-owned calls (the panel's click and the pod's own descent): a guest in
		// a live session reports the step instead of taking it. The console's own descent keeps the game's
		// semantics and never reaches this branch, and the member's OWN regeneration for the host's descent edge
		// is marked (the pod's doPod flag is still set there for the arrival effects) and must never be
		// delegated again — delegating it would suppress the very generation the host's edge asked for.
		var sessionOwned = (fromPanelEntry || fromPod) && !LayerAdvanceCoordinator.InHostEdgeDescent;
		var delegated = sessionOwned && (PatchBridge.LayerAdvance?.TryDelegateLocalAdvance(twice) ?? false);
		if (!LayerAdvancePolicy.ShouldSuppressLocalDescent(sessionOwned, delegated))
		{
			// This descent is not suppressed, so it runs on this client. On the host of a live session it is
			// the session's shared edge and must reach the members NOW — before the regeneration's own scene
			// transition reaches them as an exit — so each generates its new layer in parallel. On a guest
			// (its own pod/console descent, or no session) the port names that this is not the session's step
			// and sends nothing. The native two-layer flag (the drill pod) rides along.
			PatchBridge.LayerAdvance?.OnHostDescent(twice);
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
