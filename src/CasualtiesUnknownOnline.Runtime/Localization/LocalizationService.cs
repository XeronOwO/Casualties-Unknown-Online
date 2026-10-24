using System;
using System.Collections.Generic;
using CasualtiesUnknownOnline.Runtime.Configuration;
using Microsoft.Extensions.Options;

namespace CasualtiesUnknownOnline.Runtime.Localization;

/// <summary>
/// The key-based localization service. It reads the active language from
/// <see cref="LocalizationOptions"/> through <c>IOptionsMonitor</c>, so a
/// BepInEx config edit hot-reloads immediately. Lookup order: current language
/// table → English fallback → the key itself.
/// </summary>
public sealed class LocalizationService : ILocalizationService
{
	private string _language = "en";

	/// <summary>Languages a mod registered, each a gap-filling table over the built-in catalogue. Codes are normalised to
	/// lowercase before use, so the default ordinal comparer is the right one.</summary>
	private readonly Dictionary<string, Dictionary<string, string>> _modCatalogs = [];
	public LocalizationService(IOptionsMonitor<LocalizationOptions> options)
	{
		_language = Normalize(options.CurrentValue.Language);
		options.OnChange(next =>
		{
			var language = Normalize(next.Language);
			if (language == _language)
			{
				return;
			}

			_language = language;
			LanguageChanged?.Invoke(language);
		});
	}

	public string Language => _language;

	public event Action<string>? LanguageChanged;

	/// <summary>The built-in codes first, then a mod's registered languages in registration order.</summary>
	public IReadOnlyList<string> SupportedLanguages
	{
		get
		{
			List<string> languages = ["en", "zh"];
			foreach (var language in _modCatalogs.Keys)
			{
				var known = false;
				foreach (var existing in languages)
				{
					if (string.Equals(existing, language, StringComparison.OrdinalIgnoreCase))
					{
						known = true;
						break;
					}
				}

				if (!known)
				{
					languages.Add(language);
				}
			}

			return languages;
		}
	}

	/// <summary>
	/// Adds one mod catalogue. The merge is gap-filling: a key the language already answers (a built-in entry or an earlier
	/// registration) is left alone, so mods extend the languages instead of redefining the UI. A malformed catalogue throws
	/// from <see cref="LocalizationJson"/> with the offending construct and the file's own name in the message.
	/// </summary>
	public void RegisterCatalog(string language, string json)
	{
		if (string.IsNullOrWhiteSpace(language))
		{
			throw new ArgumentException("a catalogue needs a language code", nameof(language));
		}

		var code = language.Trim().ToLowerInvariant();
		var entries = LocalizationJson.Parse(json, $"mod catalogue '{code}'");
		if (!_modCatalogs.TryGetValue(code, out var table))
		{
			table = [];
			_modCatalogs[code] = table;
		}

		foreach (var pair in entries)
		{
			// Gap-filling only: a built-in language keeps its own text, and a repeated mod registration cannot overwrite what an
			// earlier one already supplied.
			if (LocalizationCatalog.BuiltIn.TryGetValue(code, out var builtIn) && builtIn.ContainsKey(pair.Key))
			{
				continue;
			}

			if (!table.ContainsKey(pair.Key))
			{
				table[pair.Key] = pair.Value;
			}
		}
	}

	public string T(string key)
	{
		if (_modCatalogs.TryGetValue(_language, out var modTable) && modTable.TryGetValue(key, out var modText))
		{
			return modText;
		}

		var table = TableFor(_language);
		if (table.TryGetValue(key, out var text))
		{
			return text;
		}

		if (LocalizationCatalog.English.TryGetValue(key, out var english))
		{
			return english;
		}

		return key;
	}

	public string Format(string key, params object?[] args) => string.Format(T(key), args);

	private static IReadOnlyDictionary<string, string> TableFor(string language) =>
		language == "zh" ? LocalizationCatalog.Chinese : LocalizationCatalog.English;

	/// <summary>
	/// Maps a configured code to the code this service answers in: a Chinese or English prefix keeps its built-in language, a
	/// registered mod language is accepted as-is, and anything else falls back to English (the fallback language).
	/// </summary>
	private string Normalize(string language)
	{
		var code = language.Trim().ToLowerInvariant();
		if (code.StartsWith("zh", StringComparison.Ordinal))
		{
			return "zh";
		}

		if (code.StartsWith("en", StringComparison.Ordinal))
		{
			return "en";
		}

		return _modCatalogs.ContainsKey(code) ? code : "en";
	}
}
