namespace CasualtiesUnknownOnline.Abstractions;

/// <summary>
/// The content kinds CUO binds: one constant per kind a content provider in the
/// Game Adapter materializes, and therefore the vocabulary a mod registers
/// under. Kinds are still open tags — the content policy validates the id and
/// the kind's shape, never membership of this list — so a mod may implement
/// <see cref="IModContentDefinition"/> with a kind of its own, but a
/// registration under a kind no provider claims stays opaque: the catalog and
/// the console enumerate it and nothing materializes it. The runtime binder
/// reports exactly that at load time, and a gate keeps this list equal to the
/// kinds the providers declare.
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
}
