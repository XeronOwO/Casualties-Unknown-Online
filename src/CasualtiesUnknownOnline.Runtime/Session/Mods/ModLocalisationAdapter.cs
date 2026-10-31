using System;
using System.Collections.Generic;
using System.Linq;
using CasualtiesUnknownOnline.Abstractions;
using CasualtiesUnknownOnline.Runtime.Localization;
using Microsoft.Extensions.Logging;

namespace CasualtiesUnknownOnline.Runtime.Session.Mods;

/// <summary>
/// One mod's language surface: it hands the mod's catalogue to the shared localization service under a namespace check, so a mod can only add the sentences
/// that belong to it.
/// </summary>
/// <remarks>
/// The check is the point of this type. A catalogue is one JSON object whose keys all have to start with the mod's own id, and the WHOLE catalogue is refused
/// when one key does not — a partially applied table would leave the mod half-translated in a way nobody can see. The service itself also never overwrites a
/// framework entry, so the two guards are independent: this one protects the namespace, that one protects the framework's own words.
/// </remarks>
internal sealed class ModLocalisationAdapter(
	ModManifest manifest,
	ILocalizationService localisation,
	ILogger log) : IModLocalisation
{
	public bool RegisterCatalog(string language, string json)
	{
		if (string.IsNullOrWhiteSpace(language) || string.IsNullOrWhiteSpace(json))
		{
			log.LogWarning("[Mods] {ModId} registered an empty {Language} catalogue — refused.", manifest.Id, language);
			return false;
		}

		var prefix = manifest.Id + ".";
		var keys = KeysOf(json, manifest.Id);
		var foreign = keys.Keys.FirstOrDefault(key => !key.StartsWith(prefix, StringComparison.Ordinal));
		if (foreign is not null)
		{
			log.LogWarning(
				"[Mods] {ModId} catalogue key '{Key}' is outside its namespace '{Prefix}' — the whole {Language} table is refused.",
				manifest.Id,
				foreign,
				prefix,
				language);
			return false;
		}

		try
		{
			localisation.RegisterCatalog(language, json);
		}
		catch (FormatException e)
		{
			log.LogWarning(e, "[Mods] {ModId} {Language} catalogue is not valid JSON — refused.", manifest.Id, language);
			return false;
		}

		log.LogInformation(
			"[Mods] {ModId} registered {Count} {Language} sentence(s) under '{Prefix}'.",
			manifest.Id,
			keys.Count,
			language,
			prefix);
		return true;
	}

	/// <summary>The catalogue's keys, read with the same flat-JSON reader the built-in tables use (a malformed table yields none, and the registration itself refuses it).</summary>
	private static IReadOnlyDictionary<string, string> KeysOf(string json, string source)
	{
		try
		{
			return LocalizationJson.Parse(json, source + " catalogue");
		}
		catch (FormatException)
		{
			return new Dictionary<string, string>();
		}
	}
}
