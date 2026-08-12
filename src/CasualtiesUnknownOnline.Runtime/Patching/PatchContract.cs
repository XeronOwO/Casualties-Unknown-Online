using System.Collections.Generic;
using System.Linq;

namespace CasualtiesUnknownOnline.Runtime.Patching;

/// <summary>
/// One patch contract: the target declaration a Harmony patch class binds to,
/// stringified so the contract survives without the game assembly in scope.
/// The facts are the [HarmonyPatch] attribute shape (target type + method +
/// argument types) plus the patch methods' non-special parameter names — the
/// Harmony parameter-name matching rule, whose silent break is exactly what a
/// game update causes (a renamed target parameter detaches the patch argument
/// without any error). One contract per [HarmonyPatch] class; the dynamic
/// patches (InstallDynamicPatches' reflected targets) are declared by hand.
/// The contract is the game-update guard's fact: the runtime verification
/// (PatchInventory.VerifyMissing) and the contract tests consume the SAME
/// contracts (the tests call PatchInventory.BuildContracts), so a broken
/// target fails in the test run before the game ever launches.
/// </summary>
internal sealed class PatchContract
{
	internal PatchContract(
		string patchClass,
		string targetType,
		string methodName,
		IReadOnlyList<string> parameterTypes,
		IReadOnlyList<string> patchParameters)
	{
		PatchClass = patchClass;
		TargetType = targetType;
		MethodName = methodName;
		ParameterTypes = parameterTypes;
		PatchParameters = patchParameters;
	}

	/// <summary>The patch class name (diagnostics — which hook would go silent).</summary>
	internal string PatchClass { get; }

	/// <summary>The target type's full name in the game assembly.</summary>
	internal string TargetType { get; }

	/// <summary>The target method's name.</summary>
	internal string MethodName { get; }

	/// <summary>The [HarmonyPatch] argumentTypes' full names — empty = any overload (the common shape).</summary>
	internal IReadOnlyList<string> ParameterTypes { get; }

	/// <summary>The patch methods' (Prefix/Postfix/Transpiler) parameter names that
	/// participate in Harmony's name matching — special names excluded. A name
	/// missing from the target's signature means the patch argument silently
	/// detaches.</summary>
	internal IReadOnlyList<string> PatchParameters { get; }

	/// <summary>Diagnostic rendering.</summary>
	public override string ToString() =>
		$"{PatchClass} → {TargetType}.{MethodName}({string.Join(", ", ParameterTypes.Select(t => t.Substring(t.LastIndexOf('.') + 1)))})";
}
