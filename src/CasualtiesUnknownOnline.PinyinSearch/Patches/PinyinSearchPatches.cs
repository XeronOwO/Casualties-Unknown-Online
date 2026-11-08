using System;
using System.Collections.Generic;
using System.Reflection.Emit;
using CasualtiesUnknownOnline.PinyinSearch.Core;
using HarmonyLib;

namespace CasualtiesUnknownOnline.PinyinSearch.Patches;

/// <summary>
/// Pinyin search on the game's own crafting search box. The player keeps using
/// the native field, the native list, the native "clear" button and the native
/// category buttons; only the matching rule is extended, and only while the
/// mod's switch is on.
///
/// The native filter is <c>simpleName.Contains(recipeFilter, OrdinalIgnoreCase)</c>
/// inside a compiler-generated lambda. A name hook cannot ADD a match — the
/// predicate still runs its own <c>Contains</c> on whatever name the hook
/// returns — and a patch on the BCL comparison is not reliable either (the
/// call site may already be inlined by the JIT). The extension therefore
/// rewrites the call instruction itself: the one
/// <c>string.Contains(string, StringComparison)</c> call inside
/// <c>PlayerCamera.RefreshRecipeList</c> becomes a call to
/// <see cref="PinyinContains"/>, which answers the pinyin question first while
/// a refresh scope holds a query and defers to the native comparison otherwise.
/// </summary>
internal static class PinyinSearchPatches
{
	/// <summary>
	/// Opens and closes the query scope around exactly one native refresh, and
	/// rewrites the filter's comparison so the scope can extend it.
	/// </summary>
	[HarmonyPatch(typeof(PlayerCamera), "RefreshRecipeList")]
	internal static class RefreshRecipeListPatch
	{
		private static void Prefix(PlayerCamera __instance) => RecipeSearchScope.Begin(__instance);

		private static void Postfix() => RecipeSearchScope.End();

		/// <summary>Runs even when the native body throws, so a failed refresh cannot leave the borrowed scope open into the next frame.</summary>
		private static void Finalizer() => RecipeSearchScope.End();

		/// <summary>
		/// Swaps the native filter's comparison for <see cref="PinyinContains"/>.
		/// The rewrite is what makes a pinyin-only match possible: a hook on the
		/// name (or on the BCL method) answers too late or never.
		/// </summary>
		private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
		{
			var native = AccessTools.Method(
				typeof(string),
				nameof(string.Contains),
				[typeof(string), typeof(StringComparison)]);
			var extended = AccessTools.Method(typeof(PinyinContains), nameof(PinyinContains.Contains));
			var replaced = 0;

			foreach (var instruction in instructions)
			{
				if (instruction.opcode == OpCodes.Call && Equals(instruction.operand, native))
				{
					yield return new CodeInstruction(OpCodes.Call, extended);
					replaced++;
					continue;
				}

				yield return instruction;
			}

			if (replaced == 0)
			{
				PinyinSearchGate.Log?.LogWarning(
					"[Pinyin] PlayerCamera.RefreshRecipeList no longer calls string.Contains(string, StringComparison) — "
					+ "pinyin search keeps the native filter. The predicate changed; the transpiler is stale, not clean.");
			}
			else
			{
				PinyinSearchGate.Log?.LogDebug($"[Pinyin] recipe filter comparison extended ({replaced} call site(s)).");
			}
		}
	}
}
