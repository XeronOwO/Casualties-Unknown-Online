using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using CasualtiesUnknownOnline.GameAdapter.Items;
using HarmonyLib;
using UnityEngine;
using Object = UnityEngine.Object;

namespace CasualtiesUnknownOnline.GameAdapter.Patches;

/// <summary>
/// Transpiles the native game methods that load item prefabs directly
/// through <c>Resources.Load</c> rather than <c>Utils.Create</c>: building-entity
/// death drops (<c>BuildingEntity.Update</c>), corpse loot
/// (<c>CorpseScript.Start</c>) and vanilla save restore
/// (<c>SaveSystem.TryLoadGame</c>). The resource and instantiate calls are
/// redirected through <see cref="ItemPrefabResolver"/> so CUO custom item
/// templates are served and their clones are activated; non-item resource
/// loads keep the original behavior.
/// </summary>
internal static class NativeItemResourcePatches
{
	private static readonly MethodInfo ResourcesLoadMethod = typeof(Resources)
		.GetMethods(BindingFlags.Public | BindingFlags.Static)
		.First(method =>
			method.Name == nameof(Resources.Load)
			&& !method.IsGenericMethod
			&& method.GetParameters().Length == 1
			&& method.GetParameters()[0].ParameterType == typeof(string));

	private static readonly MethodInfo LoadResourceMethod =
		AccessTools.Method(typeof(ItemPrefabResolver), nameof(ItemPrefabResolver.LoadResource));

	private static readonly MethodInfo InstantiateMethod = typeof(Object)
		.GetMethods(BindingFlags.Public | BindingFlags.Static)
		.First(method =>
			method.Name == nameof(Object.Instantiate)
			&& !method.IsGenericMethod
			&& method.GetParameters().Length == 3
			&& method.GetParameters()[0].ParameterType == typeof(Object)
			&& method.GetParameters()[1].ParameterType == typeof(Vector3)
			&& method.GetParameters()[2].ParameterType == typeof(Quaternion));

	private static readonly MethodInfo InstantiateResourceMethod =
		AccessTools.Method(typeof(ItemPrefabResolver), nameof(ItemPrefabResolver.InstantiateResource));

	private static IEnumerable<CodeInstruction> RedirectItemResourceLoads(IEnumerable<CodeInstruction> instructions)
	{
		foreach (var instruction in instructions)
		{
			if (instruction.Calls(ResourcesLoadMethod))
			{
				yield return new CodeInstruction(OpCodes.Call, LoadResourceMethod);
				continue;
			}

			if (instruction.Calls(InstantiateMethod))
			{
				yield return new CodeInstruction(OpCodes.Call, InstantiateResourceMethod);
				continue;
			}

			yield return instruction;
		}
	}

	[HarmonyPatch(typeof(BuildingEntity), "Update")]
	internal static class BuildingEntityCustomItemResourcePatch
	{
		private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions) =>
			RedirectItemResourceLoads(instructions);
	}

	[HarmonyPatch(typeof(CorpseScript), "Start")]
	internal static class CorpseScriptCustomItemResourcePatch
	{
		private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions) =>
			RedirectItemResourceLoads(instructions);
	}

	[HarmonyPatch(typeof(SaveSystem), "TryLoadGame")]
	internal static class SaveSystemCustomItemResourcePatch
	{
		private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions) =>
			RedirectItemResourceLoads(instructions);
	}
}
