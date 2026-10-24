using System;
using System.Collections.Generic;

namespace CasualtiesUnknownOnline.Runtime.Localization;

/// <summary>
/// The built-in CUO UI string tables, one embedded JSON file per language: `en.json` and `zh.json`, read once at type
/// initialisation by <see cref="LocalizationJson"/>. English is the fallback language — a lookup asks the client's language
/// first, then English, then the caller's own literal — so a language that lacks a key shows English rather than a key name.
///
/// <para>
/// The tables are DATA on purpose (the user's 2026-10-25 direction): a translator edits a file instead of C#, a new language is
/// a new file plus a registration, and a mod can ship its own catalogue through the localization service's registration point
/// without this assembly changing. Keys are stable identifiers; UI code must never pass raw English text through the
/// localization service.
/// </para>
/// </summary>
internal static class LocalizationCatalog
{
	/// <summary>The fallback language's table; also the completeness reference every other table is compared against.</summary>
	internal static readonly IReadOnlyDictionary<string, string> English =
		LocalizationJson.LoadEmbedded(typeof(LocalizationCatalog), "en.json");

	/// <summary>The second built-in language.</summary>
	internal static readonly IReadOnlyDictionary<string, string> Chinese =
		LocalizationJson.LoadEmbedded(typeof(LocalizationCatalog), "zh.json");

	/// <summary>The built-in tables by the language code the service speaks (`en`, `zh`).</summary>
	internal static readonly IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> BuiltIn =
		new Dictionary<string, IReadOnlyDictionary<string, string>>(StringComparer.OrdinalIgnoreCase)
		{
			["en"] = English,
			["zh"] = Chinese,
		};
}
