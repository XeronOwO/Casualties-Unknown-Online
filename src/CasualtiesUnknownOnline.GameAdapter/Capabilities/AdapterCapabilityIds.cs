namespace CasualtiesUnknownOnline.GameAdapter.Capabilities;

/// <summary>
/// The stable capability ids. They are the report's and the log's vocabulary
/// (and, from stage 2 on, the degradation unit), so an id is never renumbered or
/// renamed when a capability's patch set changes — a reader who greps an old log
/// must still find the same gameplay system.
/// </summary>
internal static class AdapterCapabilityIds
{
	internal const string Session = "session";
	internal const string World = "world";
	internal const string Items = "items";
	internal const string Traps = "traps";
	internal const string Medical = "medical";
	internal const string Character = "character";
	internal const string Enemies = "enemies";
	internal const string Crafting = "crafting";
	internal const string Trading = "trading";
	internal const string Save = "save";
	internal const string Tutorial = "tutorial";
	internal const string Mods = "mods";
	internal const string PinyinSearch = "pinyin-search";
	internal const string Diagnostics = "diagnostics";
}
