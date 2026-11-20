using System;
using BepInEx.Logging;
using HarmonyLib;

namespace CasualtiesUnknownOnline.Acceptance;

/// <summary>
/// The deliberate red for the <c>remote-backpack-refresh</c> row. The row's claim is that a member's open copy of
/// the owner's backpack keeps up with the owner's inventory: the game adapter materializes the clone's items in
/// <c>CloneInventoryRenderer.ApplyCloneInventory</c>, which is what gives the remote body's container the children
/// the native window draws. This prefix skips that materialization when <c>CUO_ACCEPTANCE_RED</c> names the case,
/// so the clone keeps the items it was built with and the arriving slot never reaches the open window; the row then
/// fails at the arrival it asserts.
///
/// <para>
/// Two narrower seams were tried first and both left the row green: the renderer's own
/// <c>RefreshOpenRemoteContainer</c> call (it only runs when the clone loses an item) and
/// <c>RemoteBackpackView.UpdatePendingContainerRefresh</c> (the window's buttons are rebuilt by the game itself
/// when the container's children change, so removing our rebuild changed nothing the row reads). The measurements
/// are on the ticket.
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
		Logger.LogInfo($"[RemoteViewRed] armed on {Target}.ApplyCloneInventory; it suppresses the clone render only when CUO_ACCEPTANCE_RED is {Case}.");
	}

	private static bool Prefix()
	{
		if (!string.Equals(Environment.GetEnvironmentVariable("CUO_ACCEPTANCE_RED"), Case, StringComparison.Ordinal))
		{
			return true;
		}

		Logger.LogInfo("[RemoteViewRed] the remote clone render is suppressed for the red row.");
		return false;
	}
}
