using System;

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

	/// <summary>Raised when the active language changes through config hot-reload.</summary>
	event Action<string>? LanguageChanged;

	/// <summary>Return the localized text for <paramref name="key"/>.</summary>
	string T(string key);

	/// <summary>Return the localized text for <paramref name="key"/> formatted with <paramref name="args"/>.</summary>
	string Format(string key, params object?[] args);
}
