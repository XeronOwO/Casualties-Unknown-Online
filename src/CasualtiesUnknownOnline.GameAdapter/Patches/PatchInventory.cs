using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;

namespace CasualtiesUnknownOnline.GameAdapter.Patches;

/// <summary>
/// Patch-install verification: after PatchAll, walk every [HarmonyPatch] type
/// in this assembly, resolve its target method and assert the harmony instance
/// actually patched it ("Never let a failed patch silently run" — CLAUDE.md
/// safe degradation; BodyPatches' AccessTools throw is the same idea). A game
/// update that breaks a patch target fails loud with the culprit list instead
/// of silently running unpatched — a silently missing hook is how sync bugs
/// hide (the earthquake fix depended on the SetBlock postfix being installed).
/// </summary>
internal static class PatchInventory
{
	/// <summary>
	/// Verifies every [HarmonyPatch] type in this assembly is applied by the
	/// given harmony instance. Returns the missing targets (empty = all
	/// applied). Types without a resolvable target (unexpected attribute shape)
	/// are reported as missing too — they would silently do nothing.
	/// </summary>
	/// <summary>Number of [HarmonyPatch] types in this assembly — the verification scope (one target per patch class).</summary>
	internal static int CountTargets() => typeof(PatchInventory).Assembly.GetTypes().Count(t => t.GetCustomAttribute<HarmonyPatch>() != null);

	internal static List<string> VerifyMissing(Harmony harmony)
	{
		var mine = new HashSet<MethodBase>(harmony.GetPatchedMethods().Where(m =>
			Harmony.GetPatchInfo(m) is { } info
			&& (info.Prefixes.Any(p => p.owner == harmony.Id)
				|| info.Postfixes.Any(p => p.owner == harmony.Id)
				|| info.Transpilers.Any(p => p.owner == harmony.Id))));

		var missing = new List<string>();
		foreach (var type in typeof(PatchInventory).Assembly.GetTypes())
		{
			if (type.GetCustomAttribute<HarmonyPatch>() is not { } attr)
			{
				continue;
			}

			var info = attr.info;
			var declaring = info.declaringType;
			if (declaring == null || info.methodName == null)
			{
				missing.Add($"{type.Name}: no resolvable target ({declaring?.Name ?? "?"}.{info.methodName ?? "?"})");
				continue;
			}

			var target = AccessTools.Method(declaring, info.methodName, info.argumentTypes)
				?? AccessTools.Method(declaring, info.methodName);
			if (target == null || !mine.Contains(target))
			{
				missing.Add($"{type.Name} → {declaring.Name}.{info.methodName}");
			}
		}

		return missing;
	}
}
