namespace CasualtiesUnknownOnline.Runtime.Configuration;

/// <summary>
/// The pinyin search toggle. Backed by <c>IOptionsMonitor&lt;T&gt;</c> so a
/// BepInEx config edit applies to the next search without a restart, and
/// disabled means every surface keeps the native search behavior untouched.
/// </summary>
public sealed class PinyinSearchOptions
{
	/// <summary>Whether the native search surfaces also match Chinese names by pinyin.</summary>
	public bool Enabled { get; set; }
}
