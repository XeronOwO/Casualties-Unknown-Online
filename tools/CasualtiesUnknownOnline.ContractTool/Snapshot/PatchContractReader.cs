using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Mono.Cecil;

namespace CasualtiesUnknownOnline.ContractTool.Snapshot;

/// <summary>
/// Reads the patch-target contract rows out of the adapter assembly's metadata —
/// the same rows <c>PatchInventory.BuildContracts</c> hands the runtime and the
/// contract tests, recovered without loading the adapter or the game.
///
/// The rules mirror that method exactly, because the parity gate compares the two
/// row sets: the patch class's simple name; the <c>[HarmonyPatch]</c> attribute's
/// declaring type / method name / argument types; and the patch parameter names
/// Harmony binds by name (Prefix/Postfix/Transpiler parameters minus the special
/// names and the transpiler instruction enumerable).
///
/// The one thing metadata cannot see is the hand-declared dynamic contracts,
/// which have no attribute at all (PatchInventory marks those patch classes
/// <c>"(dynamic)"</c>). They are covered by the update-day contract tests, and a
/// report names that boundary instead of pretending the lens is complete.
/// </summary>
public static class PatchContractReader
{
	private const string HarmonyPatchAttribute = "HarmonyLib.HarmonyPatch";
	private const string TranspilerInstructions = "System.Collections.Generic.IEnumerable`1<HarmonyLib.CodeInstruction>";

	private static readonly string[] SpecialParameterNames =
		["__instance", "__result", "__state", "__originalMethod", "__runOriginal", "__args"];

	private static readonly string[] PatchMethodNames = ["Prefix", "Postfix", "Transpiler"];

	/// <summary>Every <c>[HarmonyPatch]</c>-declared contract in the adapter assembly, sorted by patch class.</summary>
	public static IReadOnlyList<SnapshotContract> Read(string adapterPath)
	{
		var fullPath = Path.GetFullPath(adapterPath);
		if (!File.Exists(fullPath))
		{
			throw new FileNotFoundException($"adapter assembly not found: {fullPath}", fullPath);
		}

		using var assembly = AssemblyDefinition.ReadAssembly(fullPath, new ReaderParameters { InMemory = true });
		return
		[
			.. Flatten(assembly.MainModule.Types)
				.Select(ToContract)
				.Where(contract => contract is not null)
				.Select(contract => contract!)
				.OrderBy(contract => contract.PatchClass, StringComparer.Ordinal)
				.ThenBy(contract => contract.TargetType, StringComparer.Ordinal)
				.ThenBy(contract => contract.Method, StringComparer.Ordinal),
		];
	}

	private static SnapshotContract? ToContract(TypeDefinition patchClass)
	{
		var attribute = patchClass.CustomAttributes.FirstOrDefault(candidate => candidate.AttributeType.FullName == HarmonyPatchAttribute);
		if (attribute is null)
		{
			return null;
		}

		var declaringType = "?";
		var methodName = "?";
		IReadOnlyList<string> argumentTypes = [];
		var declaredType = false;
		var declaredMethod = false;
		foreach (var argument in attribute.ConstructorArguments)
		{
			switch (argument.Value)
			{
				case TypeReference type when !declaredType:
					declaringType = TypeNameFormat.Of(type);
					declaredType = true;
					break;
				case string name when !declaredMethod:
					methodName = name;
					declaredMethod = true;
					break;
				case CustomAttributeArgument[] types:
					argumentTypes = [.. types.Select(entry => TypeNameFormat.Of(entry.Value as TypeReference))];
					break;
			}
		}

		// PatchClass is the SIMPLE name, because that is what PatchInventory's rows and
		// the runtime's diagnostics carry (the parity gate compares them); PatchClassType
		// is the namespaced name the report identifies a row by, since 205 contracts
		// share 201 simple names.
		return new SnapshotContract(
			patchClass.Name,
			TypeNameFormat.Of(patchClass),
			declaringType,
			methodName,
			argumentTypes,
			PatchParameterNames(patchClass));
	}

	/// <summary>
	/// The patch methods' parameter names minus the names Harmony binds itself
	/// and minus the transpiler's instruction stream — the names that are matched
	/// against the target's parameters, where a game-side rename silently detaches
	/// the patch argument.
	/// </summary>
	private static IReadOnlyList<string> PatchParameterNames(TypeDefinition patchClass)
	{
		var names = new List<string>();
		foreach (var patchMethodName in PatchMethodNames)
		{
			var patchMethod = patchClass.Methods.FirstOrDefault(method => method.Name == patchMethodName && method.IsStatic);
			if (patchMethod is null)
			{
				continue;
			}

			foreach (var parameter in patchMethod.Parameters)
			{
				var name = parameter.Name;
				if (string.IsNullOrEmpty(name) || SpecialParameterNames.Contains(name, StringComparer.Ordinal))
				{
					continue;
				}

				if (patchMethodName == "Transpiler" && TypeNameFormat.Of(parameter.ParameterType) == TranspilerInstructions)
				{
					continue;
				}

				names.Add(name);
			}
		}

		return names;
	}

	private static IEnumerable<TypeDefinition> Flatten(IEnumerable<TypeDefinition> types)
	{
		foreach (var type in types)
		{
			yield return type;
			foreach (var nested in Flatten(type.NestedTypes))
			{
				yield return nested;
			}
		}
	}
}
