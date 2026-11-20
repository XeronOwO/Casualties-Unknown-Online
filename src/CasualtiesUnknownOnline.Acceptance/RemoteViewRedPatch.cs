using System;
using BepInEx.Logging;
using HarmonyLib;

namespace CasualtiesUnknownOnline.Acceptance;

/// <summary>
/// The deliberate red for the <c>remote-backpack-refresh</c> row. The row's claim is that an open remote
/// backpack keeps up with its owner's inventory: the clone renderer marks the window's refresh pending (on a
/// structure change and on the open proxy being re-created), and <c>RemoteBackpackView.UpdatePendingContainerRefresh</c>
/// rebuilds the native slot buttons through <c>PlayerCamera.RepopulateContainer</c>. This prefix skips that
/// rebuild when <c>CUO_ACCEPTANCE_RED</c> names the case, so the window keeps the slots it opened with and the
/// row fails at the arriving slot it asserts. The first seam tried the renderer's own
/// <c>RefreshOpenRemoteContainer</c> call, which only runs on removals; the arriving item re-renders the whole
/// proxy and the rebuild comes from the pending flag, so the consumer is the seam that the row actually reads.
/// Without the variable the prefix returns true and the production rebuild runs untouched, and a production
/// install never carries the acceptance assembly at all (it is deployed only with <c>-Acceptance</c>).
/// </summary>
internal static class RemoteViewRedPatch
{
	private const string Case = "remote-backpack-refresh";
	private const string Target = "CasualtiesUnknownOnline.GameAdapter.RemoteBackpackView";

	// BepInEx's own log source, because UnityEngine.Debug output did not reach either client log (measured
	// 2026-11-21: an armed seam wrote nothing while the plug-in's LogInfo line was present).
	private static readonly ManualLogSource Logger = BepInEx.Logging.Logger.CreateLogSource("CUO Acceptance Red");

	internal static void Install(Harmony harmony)
	{
		var type = AccessTools.TypeByName(Target);
		var method = type == null ? null : AccessTools.Method(type, "UpdatePendingContainerRefresh");
		if (method == null)
		{
			Logger.LogWarning($"[RemoteViewRed] no {Target}.UpdatePendingContainerRefresh to patch; the red run would read the production refresh.");
			return;
		}

		harmony.Patch(method, prefix: new HarmonyMethod(typeof(RemoteViewRedPatch), nameof(Prefix)));
		Logger.LogInfo($"[RemoteViewRed] armed on {Target}.UpdatePendingContainerRefresh; it suppresses the rebuild only when CUO_ACCEPTANCE_RED is {Case}.");
	}

	private static bool Prefix()
	{
		if (!string.Equals(Environment.GetEnvironmentVariable("CUO_ACCEPTANCE_RED"), Case, StringComparison.Ordinal))
		{
			return true;
		}

		Logger.LogInfo("[RemoteViewRed] the pending container refresh is suppressed for the red row.");
		return false;
	}
}
