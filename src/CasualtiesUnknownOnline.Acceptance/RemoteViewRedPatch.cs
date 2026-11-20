using System;
using BepInEx.Logging;
using HarmonyLib;

namespace CasualtiesUnknownOnline.Acceptance;

/// <summary>
/// The deliberate red for the <c>remote-backpack-refresh</c> row. The row's claim is that an item arriving in the
/// owner's backpack reaches a member's already-open window. The adapter's per-snapshot entry is
/// <c>CharacterDataSync</c> → <c>CloneInventoryRenderer.ApplyCloneInventory</c>, which reconciles the clone's slots
/// and the container children the native window draws. This prefix suppresses every application after the first one
/// when <c>CUO_ACCEPTANCE_RED</c> names the case: the window still has a container to open (the first application
/// built it), and the arriving item never reaches it, so the row fails at the second give it asserts.
///
/// <para>
/// Four finer seams were measured and left the row green, all recorded on the ticket: the renderer's
/// removal-only <c>RefreshOpenRemoteContainer</c> call; <c>RemoteBackpackView.UpdatePendingContainerRefresh</c>
/// (the game rebuilds the window itself when the container's children change); the whole clone render (fails at
/// "the open view shows no container", the fixture's precondition, not the claim); and
/// <c>RestoreRemoteContents</c> on an already-drawn container (the arrival turns out not to travel through it).
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
		Logger.LogInfo($"[RemoteViewRed] armed on {Target}.ApplyCloneInventory; it suppresses every application after the first only when CUO_ACCEPTANCE_RED is {Case}.");
	}

	// The first application draws the clone (and the container the fixture opens); everything after it is an
	// update, which is exactly the arriving slot the row asserts.
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
			Logger.LogInfo("[RemoteViewRed] the remote clone's later inventory applications are suppressed for the red row; the arriving slot never reaches the open window.");
		}

		return false;
	}
}
