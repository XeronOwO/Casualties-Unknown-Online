using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using CasualtiesUnknownOnline.Runtime.Patching;
using HarmonyLib;

namespace CasualtiesUnknownOnline.GameAdapter.Patches;

/// <summary>
/// Patch-install verification: after PatchAll, walk every [HarmonyPatch] type
/// in this assembly, resolve its target method and assert the harmony instance
/// actually patched it ("Never let a failed patch silently run" — AGENTS.md
/// safe degradation; BodyPatches' AccessTools throw is the same idea). A game
/// update that breaks a patch target fails loud with the culprit list instead
/// of silently running unpatched — a silently missing hook is how sync bugs
/// hide (the earthquake fix depended on the SetBlock postfix being installed).
/// The verification is two levels:
///   existence — the target method resolves and the harmony instance patched
///   it (a removed method fails here);
///   signature — the patch contract's argument types and patch-parameter names
///   still match the game's method (a renamed/re-typed method fails here, via
///   PatchContractChecker — Harmony matches patch arguments BY NAME, so a
///   renamed parameter silently detaches the patch argument).
/// The same contracts feed the contract tests (Phase 3 — the test run calls
/// BuildContracts and asserts every contract against the game assembly), so a
/// broken target fails in `dotnet test` BEFORE the game ever launches.
/// </summary>
internal static class PatchInventory
{
	/// <summary>
	/// The patch-parameter names that participate in Harmony's name matching —
	/// special names are bound by Harmony directly and excluded.
	/// </summary>
	private static readonly string[] SpecialParameterNames =
		["__instance", "__result", "__state", "__originalMethod", "__runOriginal", "__args"];

	/// <summary>Number of [HarmonyPatch] types in this assembly — the verification scope (one target per patch class).</summary>
	internal static int CountTargets() => typeof(PatchInventory).Assembly.GetTypes().Count(t => t.GetCustomAttribute<HarmonyPatch>() != null);

	/// <summary>
	/// Every patch contract in this assembly: the [HarmonyPatch] attributes
	/// (target type/method/argument types) plus the dynamic patches
	/// (InstallDynamicPatches — reflected targets with no attribute, declared
	/// by hand). The contract TESTS call this — one source of facts, so a new
	/// patch class can never be added without its contract.
	/// </summary>
	internal static List<PatchContract> BuildContracts()
	{
		var contracts = new List<PatchContract>();
		foreach (var type in typeof(PatchInventory).Assembly.GetTypes())
		{
			if (type.GetCustomAttribute<HarmonyPatch>() is { } attr)
			{
				contracts.Add(ToContract(type, attr));
			}
		}

		// The dynamic patches — reflected targets (internal game types), no
		// [HarmonyPatch] attribute: one hand-declared contract each, mirroring
		// InstallDynamicPatches' GetType/GetMethod targets. The postfix shapes
		// (object __instance / no parameters) carry no name-matching parameters.
		contracts.Add(new PatchContract("TrapCrystalPatch (dynamic)", "CrystalFragile", "Touched", [], []));
		contracts.Add(new PatchContract("TrapCrystalPatch (dynamic)", "CrystalElectric", "Shock", [], []));
		contracts.Add(new PatchContract("TrapCrystalPatch (dynamic)", "CrystalUnstable", "Update", [], []));
		contracts.Add(new PatchContract("TrapCrystalPatch (dynamic)", "CrystalUnstable", "StartTimer", [], []));
		contracts.Add(new PatchContract("TrapCrystalPatch (dynamic)", "CrystalMetamorphic", "Touched", [], []));
		contracts.Add(new PatchContract("TrapCrystalPatch (dynamic)", "CrystalShy", "Touched", [], []));
		contracts.Add(new PatchContract("TrapCrystalPatch (dynamic)", "CrystalEMP", "TryEMP", [], []));
		contracts.Add(new PatchContract("TrapCrystalPatch (dynamic)", "CrystalTeleport", "Touched", [], ["touched"]));
		contracts.Add(new PatchContract("CrystalDrippingPatch (dynamic)", "CrystalDripping", "Update", [], []));

		return contracts;
	}

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

			// With explicit argument types, a failed exact lookup must NOT fall
			// back to name-only: the fallback would silently check the wrong
			// same-name overload after a game-update type change. An
			// unconstrained contract resolves only when exactly one method
			// matches; multiple overloads are ambiguous and need explicit
			// argumentTypes, never an arbitrary pick.
			MethodInfo? target;
			if (info.argumentTypes is { Length: > 0 })
			{
				target = AccessTools.Method(declaring, info.methodName, info.argumentTypes);
			}
			else
			{
				var sameName = declaring.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static)
					.Where(m => m.Name == info.methodName)
					.ToArray();
				if (sameName.Length > 1)
				{
					missing.Add($"{type.Name}: ambiguous unconstrained contract {declaring.Name}.{info.methodName} has {sameName.Length} overloads; add [HarmonyPatch] argumentTypes.");
					continue;
				}

				target = sameName.Length == 1 ? sameName[0] : null;
			}

			if (target == null || !mine.Contains(target))
			{
				missing.Add($"{type.Name} → {declaring.Name}.{info.methodName}");
				continue;
			}

			// The signature level: the target exists and is patched, but a game
			// update may have renamed/re-typed it in a way the name-only lookup
			// silently accepts — the contract catches what existence cannot.
			missing.AddRange(PatchContractChecker.Check(ToContract(type, attr), target));
		}

		return missing;
	}

	private static PatchContract ToContract(Type patchClass, HarmonyPatch attr)
	{
		var info = attr.info;
		var parameterTypes = info.argumentTypes?
			.Select(t => t.FullName ?? t.Name).ToList()
			?? [];

		return new PatchContract(
			patchClass.Name,
			info.declaringType?.FullName ?? "?",
			info.methodName ?? "?",
			parameterTypes,
			PatchParameterNames(patchClass));
	}

	/// <summary>The patch methods' (Prefix/Postfix/Transpiler) parameter names
	/// minus the special names and the transpiler instruction enumerable — the
	/// ones Harmony matches against the target by name.</summary>
	private static List<string> PatchParameterNames(Type patchClass)
	{
		var names = new List<string>();
		foreach (var methodName in new[] { "Prefix", "Postfix", "Transpiler" })
		{
			var method = patchClass.GetMethod(methodName, BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);
			if (method == null)
			{
				continue;
			}

			foreach (var parameter in method.GetParameters())
			{
				if (SpecialParameterNames.Contains(parameter.Name)
					|| (method.Name == "Transpiler"
						&& parameter.ParameterType == typeof(IEnumerable<CodeInstruction>)))
				{
					continue;
				}

				names.Add(parameter.Name!);
			}
		}

		return names;
	}
}
