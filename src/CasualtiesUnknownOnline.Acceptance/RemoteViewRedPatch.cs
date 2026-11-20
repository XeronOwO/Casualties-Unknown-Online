using System;
using HarmonyLib;
using UDebug = UnityEngine.Debug;

namespace CasualtiesUnknownOnline.Acceptance;

/// <summary>
/// The deliberate red for the <c>remote-backpack-refresh</c> row. The row's claim is that the open remote
/// backpack keeps up with its owner's inventory, drawn through the production
/// <c>CloneInventoryRenderer.RefreshOpenRemoteContainer</c> call; this prefix skips that call when
/// <c>CUO_ACCEPTANCE_RED</c> names the case, so the window keeps its old slots and the row fails at the
/// arrival it asserts instead of passing on the clone body's own update. The variable is the whole switch:
/// without it a prefix returns true and the call runs untouched, and a production install never carries the
/// acceptance assembly at all (it is deployed only with <c>-Acceptance</c>).
/// </summary>
internal static class RemoteViewRedPatch
{
	private const string Case = "remote-backpack-refresh";
	private const string Target = "CasualtiesUnknownOnline.GameAdapter.Character.CloneInventoryRenderer";

	internal static void Install(Harmony harmony)
	{
		var type = AccessTools.TypeByName(Target);
		var method = type == null ? null : AccessTools.Method(type, "RefreshOpenRemoteContainer");
		if (method == null)
		{
			UDebug.LogWarning($"[RemoteViewRed] no {Target}.RefreshOpenRemoteContainer to patch; the red run would read the production path.");
			return;
		}

		harmony.Patch(method, prefix: new HarmonyMethod(typeof(RemoteViewRedPatch), nameof(Prefix)));
	}

	private static bool Prefix() =>
		!string.Equals(Environment.GetEnvironmentVariable("CUO_ACCEPTANCE_RED"), Case, StringComparison.Ordinal);
}
