using System;
using System.Collections.Generic;

namespace CasualtiesUnknownOnline.Runtime.Localization;

/// <summary>
/// The CUO localization surface. It provides key-based text lookup with an
/// English fallback and a config-driven language code. Pure .NET, no Unity
/// dependency — the Plugin UI and any future committed UI can share it.
/// </summary>
public interface ILocalizationService
{
	/// <summary>The currently active language code ("en" or "zh").</summary>
	string Language { get; }

	/// <summary>
	/// Every language this service can show: the built-in codes first (`en`, `zh`), then the languages mods registered, in
	/// registration order. A language picker is driven from this, so a mod's catalogue appears without a code change.
	/// </summary>
	IReadOnlyList<string> SupportedLanguages { get; }

	/// <summary>
	/// Registers a language catalogue a mod ships: a flat JSON object of key → text (the same format the built-in files use).
	/// Registration is ADDITIVE — it fills the keys that language does not have yet and never overwrites a built-in entry, so a
	/// mod cannot redefine the UI's own text. A malformed catalogue is refused by name, and re-registering the same language
	/// merges the new entries the same way.
	/// </summary>
	/// <param name="language">The language code the catalogue answers to (case-insensitive, trimmed).</param>
	/// <param name="json">The catalogue, in the built-in flat JSON format.</param>
	void RegisterCatalog(string language, string json);

	/// <summary>Raised when the active language changes through config hot-reload.</summary>
	event Action<string>? LanguageChanged;

	/// <summary>Return the localized text for <paramref name="key"/>.</summary>
	string T(string key);

	/// <summary>Return the localized text for <paramref name="key"/> formatted with <paramref name="args"/>.</summary>
	string Format(string key, params object?[] args);
}
