namespace CasualtiesUnknownOnline.Runtime.Configuration;

/// <summary>
/// The CUO UI/localization options. Backed by <c>IOptionsMonitor&lt;T&gt;</c>
/// so a BepInEx config edit hot-reloads the UI language without a restart.
/// </summary>
public sealed class LocalizationOptions
{
	/// <summary>Current language code: "en" or "zh". Unknown codes fall back to English.</summary>
	public string Language { get; set; } = "en";
}
