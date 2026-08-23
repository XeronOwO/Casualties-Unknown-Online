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

	public string T(string key)
	{
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

	private static string Normalize(string language) =>
		language.StartsWith("zh", StringComparison.OrdinalIgnoreCase) ? "zh" : "en";
}
