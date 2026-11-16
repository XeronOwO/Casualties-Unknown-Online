using System;
using System.Collections.Generic;
using HarmonyLib;

namespace CasualtiesUnknownOnline.GameAdapter.Patches;

/// <summary>
/// 1.0.2's <c>Locale.GetCharacter(string, int)</c> indexes the localized line's
/// character table by a character position and throws <see cref="IndexOutOfRangeException"/>
/// when a language table is shorter than the line it is resolving. The native
/// <c>Talker.Update</c> calls this for every spoken mood line; the failed lookup is a
/// missing glyph, not gameplay state. Return a single empty line for that one failed
/// lookup: <c>Talker.Talk</c> indexes the returned list, so an empty list would replace
/// the missing glyph with an index error of its own. Every other failure still reaches
/// the caller.
/// </summary>
internal static class LocaleGetCharacterPatch
{
	[HarmonyPatch(typeof(Locale), nameof(Locale.GetCharacter), [typeof(string), typeof(int)])]
	internal static class GetCharacterFinalizer
	{
		private static Exception? Finalizer(Exception? __exception, ref List<string> __result)
		{
			if (__exception is not IndexOutOfRangeException)
			{
				return __exception;
			}

			__result = [string.Empty];
			return null;
		}
	}
}
