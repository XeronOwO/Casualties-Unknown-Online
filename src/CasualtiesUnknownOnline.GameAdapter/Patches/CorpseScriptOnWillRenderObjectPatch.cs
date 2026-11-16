using System;
using HarmonyLib;

namespace CasualtiesUnknownOnline.GameAdapter.Patches;

/// <summary>
/// 1.0.2's <c>CorpseScript.OnWillRenderObject</c> reads the localized corpse-comment
/// lines through <c>Locale.GetCharacter(string)</c>; one of those strings has more
/// format characters than the locale table can index, and the render callback throws
/// <see cref="IndexOutOfRangeException"/>. The corpse visual has already marked
/// <c>didComment</c> at that point, so the failed line is a one-frame localization
/// miss, not a gameplay state transition. Drop only that exception; anything else in
/// the callback still surfaces.
/// </summary>
internal static class CorpseScriptOnWillRenderObjectPatch
{
	[HarmonyPatch(typeof(CorpseScript), "OnWillRenderObject")]
	internal static class OnWillRenderObjectFinalizer
	{
		private static Exception? Finalizer(Exception? __exception) =>
			__exception is IndexOutOfRangeException ? null : __exception;
	}
}
