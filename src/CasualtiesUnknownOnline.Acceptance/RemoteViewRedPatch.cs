using System;
using BepInEx.Logging;
using HarmonyLib;

namespace CasualtiesUnknownOnline.Acceptance;

/// <summary>
/// The deliberate red for the <c>remote-backpack-refresh</c> row. The row's claim is that an item arriving in the
/// owner's backpack reaches a member's already-open window. The member's copy of that backpack is a display proxy
/// the adapter marks with <c>RemoteInventoryItemId</c>, and the item that arrives is loaded into it through the
/// game's own <c>Container.LoadItem</c>. This prefix refuses that load when the proxy container already holds a
/// child and <c>CUO_ACCEPTANCE_RED</c> names the case: the warm-up give lands, the arriving give never does, and
/// the open window keeps the slots it had — which is the second give the row asserts.
///
/// <para>
/// Seven other seams were measured and are recorded on the ticket: the renderer's removal-only
/// <c>RefreshOpenRemoteContainer</c>; <c>RemoteBackpackView.UpdatePendingContainerRefresh</c> (the game rebuilds
/// the window itself); the whole clone render; <c>RestoreRemoteContents</c> on a drawn container; every clone
/// application after the first; every clone application after the clone's container is drawn; and every clone
/// application once the open window draws a slot. They name the finding: the arriving item does not travel through
/// the clone render at all — it is loaded into the proxy container by the item-state path.
/// </para>
///
/// <para>
/// Without the variable the prefix returns true and the production path runs untouched, and a production install
/// never carries the acceptance assembly at all (it is deployed only with <c>-Acceptance</c>).
/// </para>
/// </summary>
internal static class RemoteViewRedPatch
{
	private const string Case = "remote-backpack-refresh";
	private const string Marker = "CasualtiesUnknownOnline.GameAdapter.Character.RemoteInventoryItemId";

	// BepInEx's own log source, because UnityEngine.Debug output did not reach either client log (measured
	// 2026-11-21: an armed seam wrote nothing while the plug-in's LogInfo line was present).
	private static readonly ManualLogSource Logger = BepInEx.Logging.Logger.CreateLogSource("CUO Acceptance Red");

	private static Type? _marker;
	private static bool _suppressionLogged;

	internal static void Install(Harmony harmony)
	{
		var loadItem = AccessTools.Method(typeof(Container), "LoadItem", [typeof(Item)]);
		if (loadItem == null)
		{
			Logger.LogWarning("[RemoteViewRed] no Container.LoadItem(Item) to patch; the red run would read the production load.");
			return;
		}

		_marker = AccessTools.TypeByName(Marker);
		if (_marker == null)
		{
			Logger.LogWarning($"[RemoteViewRed] no {Marker} on this build; the red row would read the production load.");
			return;
		}

		harmony.Patch(loadItem, prefix: new HarmonyMethod(typeof(RemoteViewRedPatch), nameof(Prefix)));
		Logger.LogInfo($"[RemoteViewRed] armed on Container.LoadItem; it refuses a marked proxy container's second child only when CUO_ACCEPTANCE_RED is {Case}.");
	}

	// A proxy container that already holds one child is the state the arriving give changes; refusing from there
	// keeps the fixture's precondition (a container, a warm-up slot) and breaks exactly the row's claim.
	private static bool Prefix(Container __instance, Item item)
	{
		if (!string.Equals(Environment.GetEnvironmentVariable("CUO_ACCEPTANCE_RED"), Case, StringComparison.Ordinal))
		{
			return true;
		}

		if (__instance == null || item == null || _marker == null) // Unity objects — ==
		{
			return true;
		}

		if (__instance.GetComponent(_marker) == null || __instance.transform.childCount == 0) // Unity objects — ==
		{
			return true;
		}

		if (!_suppressionLogged)
		{
			_suppressionLogged = true;
			Logger.LogInfo("[RemoteViewRed] the member's proxy container already draws a slot; the arriving give's load is refused for the red row.");
		}

		return false;
	}
}
