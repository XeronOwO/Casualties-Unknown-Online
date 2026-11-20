using System;
using BepInEx.Logging;
using HarmonyLib;

namespace CasualtiesUnknownOnline.Acceptance;

/// <summary>
/// The deliberate red for the <c>remote-backpack-refresh</c> row. The row's claim is that an item arriving in the
/// owner's backpack reaches a member's already-open window. This prefix skips
/// <c>CloneInventoryRenderer.ApplyCloneInventory</c> — the adapter's per-snapshot render of the member's copy of
/// the owner's body — when <c>CUO_ACCEPTANCE_RED</c> names the case, so the member's copy never materializes the
/// backpack container and the row cannot open the view it asserts. That is where the row fails.
///
/// <para>
/// Seven finer seams were measured first and are recorded on the ticket, because what they measured is the finding:
/// the renderer's removal-only <c>RefreshOpenRemoteContainer</c>; <c>RemoteBackpackView.UpdatePendingContainerRefresh</c>
/// (the game rebuilds the window itself); <c>RestoreRemoteContents</c> on a drawn container; every application after
/// the clone's first; every application after the clone's container is drawn; every application once the open window
/// draws a slot; and a refused <c>Container.LoadItem</c> into a second child of a marked proxy. All seven left the row
/// green, and none of the guards ever matched, which means the arriving item reaches the open window through a path
/// these seams do not name. A case-scoped break that keeps the row's precondition therefore has to be found from that
/// path, not guessed at from the render; the ticket carries that as the open question, and this seam demonstrates the
/// row fails when the member's copy is not rendered at all.
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

	private static bool Prefix()
	{
		if (!string.Equals(Environment.GetEnvironmentVariable("CUO_ACCEPTANCE_RED"), Case, StringComparison.Ordinal))
		{
			return true;
		}

		if (!_suppressionLogged)
		{
			_suppressionLogged = true;
			Logger.LogInfo("[RemoteViewRed] the member's copy of the owner's body is not rendered for the red row; the row cannot open the view it asserts.");
		}

		return false;
	}
}
