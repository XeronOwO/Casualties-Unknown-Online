namespace CasualtiesUnknownOnline.Runtime.Configuration;

/// <summary>
/// The pinyin search toggle. Backed by <c>IOptionsMonitor&lt;T&gt;</c> so a
/// BepInEx config edit applies to the next search without a restart. Disabled
/// means no surface adds pinyin matching: the crafting search box keeps the
/// game's own substring rule, and the console's resource completion keeps its
/// catalog ranking (exact id, id prefix, bare path prefix, display-name prefix).
/// </summary>
public sealed class PinyinSearchOptions
{
	/// <summary>Whether the native search surfaces also match Chinese names by pinyin.</summary>
	public bool Enabled { get; set; }
}
