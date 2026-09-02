namespace CasualtiesUnknownOnline.Abstractions;

/// <summary>
/// Common content kind names for mods migrating from library-style content
/// systems. Content kinds are still mod-defined tags; these constants are the
/// base vocabulary the framework's content catalog and future content binding
/// layers can validate and display without interpreting mod payloads.
/// </summary>
public static class ModContentKind
{
	/// <summary>A static item/equipment/usable definition.</summary>
	public const string Item = "item";

	/// <summary>A crafting or cooking recipe definition.</summary>
	public const string Recipe = "recipe";

	/// <summary>A logical liquid definition.</summary>
	public const string Liquid = "liquid";

	/// <summary>A world-fluid liquid-tile definition.</summary>
	public const string LiquidTile = "liquidtile";

	/// <summary>A terrain/block tile definition.</summary>
	public const string Tile = "tile";

	/// <summary>A world building entity definition.</summary>
	public const string Building = "building";

	/// <summary>An authored multi-block structure definition.</summary>
	public const string Structure = "structure";

	/// <summary>A body/limb status definition.</summary>
	public const string Status = "status";

	/// <summary>A player-visible status/moodle definition.</summary>
	public const string Moodle = "moodle";

	/// <summary>A native settings/configuration option definition.</summary>
	public const string Setting = "setting";

	/// <summary>A localization entry or locale set definition.</summary>
	public const string Locale = "locale";
}
