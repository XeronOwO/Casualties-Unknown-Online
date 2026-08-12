using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace CasualtiesUnknownOnline.Runtime.Patching;

/// <summary>
/// The patch-contract verdict logic — the game-update guard's decision side.
/// Input: a <see cref="PatchContract"/> (what a patch class binds to) and the
/// method the resolver found in the game assembly (null = not found). Output:
/// the violations — the exact list of hooks a game update broke. The resolver
/// (game-side AccessTools, test-side reflection) is a query and stays at the
/// call sites; this class holds only the comparison:
///   1. the target method exists (a missing method is a missing hook — Harmony
///      would fail the whole PatchAll, but the contract test must name it
///      before the game ever launches);
///   2. when the contract constrains argument types, they match EXACTLY (the
///      [HarmonyPatch] argumentTypes shape — a renamed/ret-typed overload must
///      not be silently picked up by a name-only fallback);
///   3. every patch parameter name exists on the target (Harmony matches patch
///      arguments BY NAME — a renamed target parameter silently detaches the
///      patch argument, the failure mode no existence check can see).
/// </summary>
internal static class PatchContractChecker
{
	internal static List<string> Check(PatchContract contract, MethodInfo? target)
	{
		var violations = new List<string>();
		if (target == null)
		{
			violations.Add($"{contract.PatchClass}: target {contract.TargetType}.{contract.MethodName} not found");
			return violations;
		}

		if (contract.ParameterTypes.Count > 0)
		{
			var actual = target.GetParameters().Select(p => p.ParameterType.FullName ?? p.ParameterType.Name).ToArray();
			if (actual.Length != contract.ParameterTypes.Count)
			{
				violations.Add($"{contract.PatchClass}: {contract.TargetType}.{contract.MethodName} expects {contract.ParameterTypes.Count} parameter(s), game has {actual.Length}");
			}
			else
			{
				for (var i = 0; i < contract.ParameterTypes.Count; i++)
				{
					if (actual[i] != contract.ParameterTypes[i])
					{
						violations.Add($"{contract.PatchClass}: parameter[{i}] type mismatch — contract '{contract.ParameterTypes[i]}', game '{actual[i]}'");
					}
				}
			}
		}

		if (contract.PatchParameters.Count > 0)
		{
			var targetNames = new HashSet<string>(target.GetParameters().Select(p => p.Name).Where(n => n != null).Select(n => n!));
			foreach (var name in contract.PatchParameters)
			{
				if (!targetNames.Contains(name))
				{
					violations.Add($"{contract.PatchClass}: patch parameter '{name}' missing from {contract.TargetType}.{contract.MethodName} (renamed?)");
				}
			}
		}

		return violations;
	}
}
