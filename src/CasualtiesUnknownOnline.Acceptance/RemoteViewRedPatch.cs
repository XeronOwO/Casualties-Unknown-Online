using System;
using BepInEx.Logging;
using HarmonyLib;

namespace CasualtiesUnknownOnline.Acceptance;

/// <summary>
/// The deliberate red for the <c>remote-backpack-refresh</c> row. The row's claim is that an item arriving in the
/// owner's backpack reaches a member's already-open window. The traced path (2026-11-21) is
/// <c>CloneInventoryRenderer.ApplyCloneInventory</c>: the member's drawn copy of the owner's body goes 4 → 5 items as
/// the item arrives, while <c>Container.LoadItem</c> and <c>RemoteItemSceneOps.BindToContainer</c> never fire.
/// This prefix lets the first population through (it creates the container the window shows) and refuses every later
/// application when <c>CUO_ACCEPTANCE_RED</c> names the case, so the arriving item never reaches the drawn copy.
///
/// <para>
/// Seven narrower seams were measured before the trace, and what they measured is recorded on the ticket: the
/// renderer's removal-only <c>RefreshOpenRemoteContainer</c>; <c>RemoteBackpackView.UpdatePendingContainerRefresh</c>
/// (the game rebuilds the window itself); <c>RestoreRemoteContents</c> on a drawn container; every application after
/// the clone's first; every application after the clone's container is drawn; every application once the open window
/// draws a slot; and a refused <c>Container.LoadItem</c> into a second child of a marked proxy.
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
		var method = type == null ? null : AccessTools.Method(type, "ApplyCloneInventory");
		if (method == null)
		{
			Logger.LogWarning($"[RemoteViewRed] no {Target}.ApplyCloneInventory to patch; the red run would read the production render.");
			return;
		}

		harmony.Patch(method, prefix: new HarmonyMethod(typeof(RemoteViewRedPatch), nameof(Prefix)));
		Logger.LogInfo($"[RemoteViewRed] armed on {Target}.ApplyCloneInventory; it skips the member's copy of the owner's body only when CUO_ACCEPTANCE_RED is {Case}.");
	}

	/// <summary>
	/// The seam keeps the clone's first population — that is what creates the container the open window shows — and
	/// refuses every later application, so a snapshot that carries a newly arrived item never reaches the drawn copy.
	/// The row then fails on the arrival it asserts, with its precondition intact.
	/// </summary>
	private static bool Prefix(Body clone)
	{
		if (!string.Equals(Environment.GetEnvironmentVariable("CUO_ACCEPTANCE_RED"), Case, StringComparison.Ordinal))
		{
			return true;
		}

		if (clone == null || clone.GetComponentsInChildren<Item>(true).Length == 0) // Unity objects — ==
		{
			return true;
		}

		if (!_suppressionLogged)
		{
			_suppressionLogged = true;
			Logger.LogInfo("[RemoteViewRed] the drawn copy keeps its first population; later snapshots are not applied for the red row.");
		}

		return false;
	}
}
