using System;
using System.Collections;
using System.Reflection;
using BepInEx.Logging;
using HarmonyLib;

namespace CasualtiesUnknownOnline.Acceptance;

/// <summary>
/// The deliberate red for the <c>remote-backpack-refresh</c> row. The row's claim is that an item arriving in the
/// owner's backpack reaches a member's already-open window. The adapter's per-snapshot entry,
/// <c>CloneInventoryRenderer.ApplyCloneInventory</c>, is what reconciles the clone's container children with the
/// owner's inventory; the native window's buttons are rebuilt by the game from those children. This prefix stops the
/// reconciliation once the open window already draws at least one slot when <c>CUO_ACCEPTANCE_RED</c> names the
/// case, so the window keeps the slot the warm-up give put there and the arriving give never appears — which is the
/// second give the row asserts.
///
/// <para>
/// Six finer seams were measured and left the row green or failed it at its precondition, all recorded on the
/// ticket: the renderer's removal-only <c>RefreshOpenRemoteContainer</c>; <c>RemoteBackpackView.UpdatePendingContainerRefresh</c>
/// (the game rebuilds the window itself); the whole clone render (fails at "the open view shows no container");
/// <c>RestoreRemoteContents</c> on a drawn container (the arrival does not travel through it); every application after
/// the clone's first (the backpack container is drawn by the same application that carries the arrival); and every
/// application after the clone's container is drawn (the container proxy is re-created by that same application).
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

	private static FieldInfo? _windowButtons;
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

		_windowButtons = AccessTools.Field(typeof(PlayerCamera), "containerUnloadButtons");
		if (_windowButtons == null)
		{
			Logger.LogWarning("[RemoteViewRed] PlayerCamera.containerUnloadButtons is missing on this build; the red row would read the production refresh.");
			return;
		}

		harmony.Patch(method, prefix: new HarmonyMethod(typeof(RemoteViewRedPatch), nameof(Prefix)));
		Logger.LogInfo($"[RemoteViewRed] armed on {Target}.ApplyCloneInventory; it stops reconciling once the open window draws a slot, and only when CUO_ACCEPTANCE_RED is {Case}.");
	}

	// An open window that already draws one slot is the state the arriving give changes; suppressing from there keeps
	// the fixture's precondition (a container to open, a warm-up slot) and breaks exactly the claim.
	private static bool Prefix()
	{
		if (!string.Equals(Environment.GetEnvironmentVariable("CUO_ACCEPTANCE_RED"), Case, StringComparison.Ordinal))
		{
			return true;
		}

		var camera = PlayerCamera.main;
		if (camera == null) // Unity object — ==
		{
			return true;
		}

		var buttons = _windowButtons?.GetValue(camera) as IList;
		if (buttons == null || buttons.Count == 0)
		{
			return true;
		}

		if (!_suppressionLogged)
		{
			_suppressionLogged = true;
			Logger.LogInfo("[RemoteViewRed] the open window already draws a slot; the clone's later reconciliation is suppressed for the red row, so the arriving give never appears.");
		}

		return false;
	}
}
