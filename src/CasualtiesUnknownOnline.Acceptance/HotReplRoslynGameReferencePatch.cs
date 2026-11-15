using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using BepInEx;
using HarmonyLib;
using UnityEngine;

namespace CasualtiesUnknownOnline.Acceptance;

/// <summary>
/// 1.0.2 removed Mono.CSharp, so HotRepl runs its Roslyn evaluator. That evaluator builds its
/// ScriptOptions from the AppDomain's assemblies and skips any assembly whose Location is empty.
/// Unity loads Assembly-CSharp from memory, so Location is empty and a recipe that names a game type
/// (PlayerCamera, Item, ...) fails with CS0103. Add the on-disk assembly as a metadata reference.
///
/// This is acceptance-channel plumbing only: the production plugin never loads this assembly.
/// The patch is reflection-only on HotRepl's side so this project does not take a Roslyn reference.
/// </summary>
internal static class HotReplRoslynGameReferencePatch
{
	private static bool _installed;

	internal static void TryInstall(Harmony harmony)
	{
		if (_installed)
		{
			return;
		}

		var evaluator = AppDomain.CurrentDomain.GetAssemblies()
			.Select(assembly => assembly.GetType("HotRepl.Evaluator.Roslyn.RoslynScriptEvaluator", throwOnError: false))
			.FirstOrDefault(type => type != null);
		var buildOptions = evaluator?.GetMethod("BuildOptions", BindingFlags.Instance | BindingFlags.NonPublic);
		if (buildOptions == null)
		{
			return;
		}

		harmony.Patch(buildOptions, postfix: new HarmonyMethod(typeof(HotReplRoslynGameReferencePatch), nameof(Postfix)));
		_installed = true;
		Debug.Log("[Acceptance] HotRepl Roslyn evaluator now references the on-disk game assembly.");
	}

	private static object? Postfix(object __result)
	{
		if (__result == null)
		{
			return null;
		}

		var gameAssembly = Path.Combine(Paths.ManagedPath ?? Path.Combine(Paths.GameRootPath, "CasualtiesUnknown_Data", "Managed"), "Assembly-CSharp.dll");
		if (!File.Exists(gameAssembly))
		{
			Debug.LogWarning("[Acceptance] game assembly not found for the Roslyn reference: " + gameAssembly);
			return __result;
		}

		var metadataReference = AppDomain.CurrentDomain.GetAssemblies()
			.Select(assembly => assembly.GetType("Microsoft.CodeAnalysis.MetadataReference", throwOnError: false))
			.FirstOrDefault(type => type != null);
		var createFromFile = metadataReference == null ? null : metadataReference.GetMethod("CreateFromFile", [typeof(string)]);
		if (createFromFile == null)
		{
			Debug.LogWarning("[Acceptance] Microsoft.CodeAnalysis.MetadataReference.CreateFromFile(string) not found; type=" + (metadataReference?.AssemblyQualifiedName ?? "(null)"));
			return __result;
		}

		var reference = createFromFile.Invoke(null, [gameAssembly]);
		if (reference == null)
		{
			return __result;
		}

		var addReferences = __result.GetType()
			.GetMethods(BindingFlags.Public | BindingFlags.Instance)
			.FirstOrDefault(method => method.Name == "AddReferences" && method.GetParameters().Length == 1);
		if (addReferences == null)
		{
			Debug.LogWarning("[Acceptance] ScriptOptions.AddReferences not found.");
			return __result;
		}

		var parameterType = addReferences.GetParameters()[0].ParameterType;
		object argument;
		if (parameterType.IsArray)
		{
			var array = Array.CreateInstance(parameterType.GetElementType()!, 1);
			array.SetValue(reference, 0);
			argument = array;
		}
		else
		{
			var elementType = parameterType.GetGenericArguments().FirstOrDefault();
			if (elementType == null)
			{
				return __result;
			}

			var listType = typeof(List<>).MakeGenericType(elementType);
			var list = (IList)Activator.CreateInstance(listType)!;
			list.Add(reference);
			argument = list;
		}

		Debug.Log("[Acceptance] Roslyn ScriptOptions now reference " + gameAssembly + ".");
		return addReferences.Invoke(__result, [argument]);
	}
}
