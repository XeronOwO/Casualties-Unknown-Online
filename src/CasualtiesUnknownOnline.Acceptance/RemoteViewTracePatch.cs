using System;
using System.Reflection;
using BepInEx.Logging;
using HarmonyLib;

namespace CasualtiesUnknownOnline.Acceptance;

/// <summary>
/// A read-only trace of the paths an item can take into a member's copy of someone else's backpack, armed only by
/// <c>CUO_ACCEPTANCE_TRACE=1</c>. It exists because the <c>remote-backpack-refresh</c> row's red seam could not be
/// placed on a guess: seven candidate seams never fired, so the arrival's real path has to be read rather than
/// assumed (2026-11-21, the measurements are on the ticket).
///
/// <para>
/// Each line names the entry and what it saw: the clone render's call with how many items the clone already draws,
/// the container load with the container's marked id and its drawn child count, and the item bind with the parent
/// item id. Nothing is written without the variable, and a production install never carries the assembly.
/// </para>
/// </summary>
internal static class RemoteViewTracePatch
{
	private static readonly ManualLogSource Logger = BepInEx.Logging.Logger.CreateLogSource("CUO Acceptance Trace");

	private static Type? _marker;

	internal static void Install(Harmony harmony)
	{
		if (!string.Equals(Environment.GetEnvironmentVariable("CUO_ACCEPTANCE_TRACE"), "1", StringComparison.Ordinal))
		{
			return;
		}

		_marker = AccessTools.TypeByName("CasualtiesUnknownOnline.GameAdapter.Character.RemoteInventoryItemId");
		var renderer = AccessTools.TypeByName("CasualtiesUnknownOnline.GameAdapter.Character.CloneInventoryRenderer");
		var apply = renderer == null ? null : AccessTools.Method(renderer, "ApplyCloneInventory");
		var load = AccessTools.Method(typeof(Container), "LoadItem", [typeof(Item)]);
		var bind = AccessTools.TypeByName("CasualtiesUnknownOnline.GameAdapter.Items.RemoteItemSceneOps") is { } scene
			? AccessTools.Method(scene, "BindToContainer")
			: null;

		var installed = 0;
		installed += Patch(harmony, apply, nameof(TraceApply), "ApplyCloneInventory");
		installed += Patch(harmony, load, nameof(TraceLoad), "Container.LoadItem");
		installed += Patch(harmony, bind, nameof(TraceBind), "RemoteItemSceneOps.BindToContainer");
		Logger.LogInfo($"[RemoteViewTrace] armed on {installed} of 3 paths; the arrival's route is written below.");
	}

	private static int Patch(Harmony harmony, MethodBase? method, string name, string label)
	{
		if (method == null)
		{
			Logger.LogWarning($"[RemoteViewTrace] no {label} to trace; that path stays unread.");
			return 0;
		}

		harmony.Patch(method, prefix: new HarmonyMethod(typeof(RemoteViewTracePatch), name));
		return 1;
	}

	private static void TraceApply(Body? clone) =>
		Logger.LogInfo($"[RemoteViewTrace] ApplyCloneInventory clone {clone?.name ?? "null"} draws {Drawn(clone)} item(s).");

	private static void TraceLoad(Container? __instance, Item? item) =>
		Logger.LogInfo($"[RemoteViewTrace] Container.LoadItem container {InstanceId(__instance)} children {__instance?.transform.childCount ?? 0} item {item?.id ?? "null"}.");

	private static void TraceBind(Item? item, ulong parentItemId) =>
		Logger.LogInfo($"[RemoteViewTrace] BindToContainer item {item?.id ?? "null"} into parent {parentItemId}.");

	private static int Drawn(Body? clone) => clone == null ? 0 : clone.GetComponentsInChildren<Item>(true).Length;

	private static string InstanceId(Container? container)
	{
		if (container == null || _marker == null) // Unity objects — ==
		{
			return "0";
		}

		var marker = container.GetComponent(_marker);
		return marker == null ? "0" : AccessTools.Field(_marker, "Id")?.GetValue(marker)?.ToString() ?? "0";
	}
}
