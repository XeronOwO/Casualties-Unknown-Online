using System;
using BepInEx.Logging;
using HarmonyLib;

namespace CasualtiesUnknownOnline.Acceptance;

/// <summary>
/// The deliberate red for the <c>remote-backpack-refresh</c> row. The row's claim is that an item arriving in the
/// owner's backpack while a member has that backpack open reaches the open window. The adapter materializes the
/// clone's container children in <c>CloneInventoryRenderer.RestoreRemoteContents</c>; the first render (an empty
/// container) must run so the window has something to open, and every later update is the arriving slot itself.
/// This prefix suppresses the later updates when <c>CUO_ACCEPTANCE_RED</c> names the case, so the window keeps the
/// slots it opened with and the row fails at the second give it asserts.
///
/// <para>
/// Two coarser seams left the row green and are recorded on the ticket: the renderer's removal-only
/// <c>RefreshOpenRemoteContainer</c> call, and <c>RemoteBackpackView.UpdatePendingContainerRefresh</c> (the game
/// rebuilds the window itself when the container's children change). Skipping the whole clone render also fails the
/// row, but at "the open view shows no container", which is the fixture's precondition rather than the claim.
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
	private const string Target = "CasualtiesUnknownOnline.GameAdapter.Character.CloneInventoryRenderer";

	// BepInEx's own log source, because UnityEngine.Debug output did not reach either client log (measured
	// 2026-11-21: an armed seam wrote nothing while the plug-in's LogInfo line was present).
	private static readonly ManualLogSource Logger = BepInEx.Logging.Logger.CreateLogSource("CUO Acceptance Red");

	private static bool _suppressionLogged;

	internal static void Install(Harmony harmony)
	{
		var type = AccessTools.TypeByName(Target);
		var method = type == null ? null : AccessTools.Method(type, "RestoreRemoteContents");
		if (method == null)
		{
			Logger.LogWarning($"[RemoteViewRed] no {Target}.RestoreRemoteContents to patch; the red run would read the production render.");
			return;
		}

		harmony.Patch(method, prefix: new HarmonyMethod(typeof(RemoteViewRedPatch), nameof(Prefix)));
		Logger.LogInfo($"[RemoteViewRed] armed on {Target}.RestoreRemoteContents; it suppresses an already-drawn container's updates only when CUO_ACCEPTANCE_RED is {Case}.");
	}

	// The first render of a container has no drawn children; suppressing only the later calls keeps the window's
	// precondition (a container to open) intact and breaks exactly the arriving slot the row asserts.
	private static bool Prefix(Item containerItem)
	{
		if (!string.Equals(Environment.GetEnvironmentVariable("CUO_ACCEPTANCE_RED"), Case, StringComparison.Ordinal))
		{
			return true;
		}

		var container = containerItem == null ? null : containerItem.GetComponent<Container>();
		if (container == null || container.transform.childCount == 0) // Unity objects — ==
		{
			return true;
		}

		if (!_suppressionLogged)
		{
			_suppressionLogged = true;
			Logger.LogInfo("[RemoteViewRed] a drawn remote container's update is suppressed for the red row; the arriving slot never reaches the open window.");
		}

		return false;
	}
}
