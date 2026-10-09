using System.Collections.Generic;

namespace CasualtiesUnknownOnline.Abstractions;

/// <summary>
/// The mod-authored definition of one item content. It is deliberately a plain
/// data object in Abstractions: no game type, no Unity type, no Runtime
/// dependency. A mod fills it in and registers it through
/// <see cref="IModContent"/>; the Runtime content binder routes it by
/// <see cref="Kind"/> to the Game Adapter provider that materializes it, and
/// that provider reads these typed members instead of decoding a private format.
/// </summary>
public sealed class ModItemDefinition : IModContentDefinition
{
	/// <summary>The mod-scoped content id: a canonical lower-case path segment, unique within the registering mod.</summary>
	public string Id { get; set; } = "";

	/// <summary>The content kind this definition registers under - fixed by its type, never chosen by a caller.</summary>
	public string Kind => ModContentKind.Item;

	/// <summary>The mod-owned content schema version the framework stores verbatim (default 1).</summary>
	public int SchemaVersion { get; set; } = 1;

	/// <summary>Player-facing item name.</summary>
	public string DisplayName { get; set; } = "";

	/// <summary>Player-facing item description.</summary>
	public string Description { get; set; } = "";

	/// <summary>Vanilla spawn/category tag; defaults to "nospawn" when empty.</summary>
	public string Category { get; set; } = "nospawn";

	/// <summary>Item weight in vanilla units.</summary>
	public float Weight { get; set; }

	/// <summary>Vanilla item value.</summary>
	public int Value { get; set; }

	/// <summary>Whether the item can be used from the hand.</summary>
	public bool Usable { get; set; }

	/// <summary>Whether the item can be used with the left mouse button.</summary>
	public bool UsableWithLmb { get; set; }

	/// <summary>Whether the item can be worn on a body.</summary>
	public bool Wearable { get; set; }

	/// <summary>Whether the item is destroyed when its condition reaches zero.</summary>
	public bool DestroyAtZeroCondition { get; set; }

	/// <summary>Vanilla tag string, empty when none.</summary>
	public string Tags { get; set; } = "";

	/// <summary>Relative spawn/trader/loot weighting.</summary>
	public int SpawnFrequency { get; set; } = 1;

	/// <summary>
	/// The vanilla prefab id used as the runtime template base. Empty means the
	/// definition is static-item-info only; the Game Adapter cannot materialize
	/// a prefab for it.
	/// </summary>
	public string TemplateId { get; set; } = "";

	/// <summary>
	/// Component type names (assembly-qualified or simple names) attached to the
	/// runtime template before it is instantiated. The Game Adapter resolves
	/// the types from loaded assemblies and refuses non-Component types.
	/// </summary>
	public List<string> SpawnComponents
	{
		get;
		set => field = value ?? [];
	} = [];

	/// <summary>Extensible mod-owned metadata for future binders/features.</summary>
	public Dictionary<string, string> CustomData
	{
		get;
		set => field = value ?? [];
	} = [];

	/// <summary>
	/// Average loose world-spawn count per worldgen chunk. Null or zero disables
	/// automatic world spawning; a positive value makes the Game Adapter scatter
	/// the item on ground inside the isolated generation stream. The existing
	/// generation-item snapshot synchronizes both sides — no new wire is needed.
	/// </summary>
	public float? WorldSpawnPerChunk { get; set; }

	/// <summary>
	/// Optional explicit fixed drop-source pools. When set, the item is not added
	/// to the generic vanilla category loot pool; instead it is registered only in
	/// the selected source pools (corpse, built-in crates, trader stock). Leave
	/// null to use the vanilla category fallback.
	/// </summary>
	public ModItemDropSource? DropSources { get; set; }

	/// <summary>Optional container behavior applied to the runtime item template.</summary>
	public ModItemContainer? Container { get; set; }

	/// <summary>Optional battery behavior applied to the runtime item template.</summary>
	public ModItemBattery? Battery { get; set; }

	/// <summary>Optional light behavior applied to the runtime item template.</summary>
	public ModItemLight? Light { get; set; }

	/// <summary>Optional melee/tool behavior applied to the item's static use action.</summary>
	public ModItemTool? Tool { get; set; }

	/// <summary>Optional firearm behavior applied to the runtime item template and static use action.</summary>
	public ModItemGun? Gun { get; set; }

	/// <summary>
	/// Vanilla decay time in in-game minutes. Zero disables time-based decay;
	/// a positive value also sets the computed <c>rotSpeed</c> used by the
	/// vanilla decay path (including battery-powered drain when
	/// <see cref="Battery"/> is present).
	/// </summary>
	public float DecayMinutes { get; set; }

	/// <summary>Optional visual presentation (worn sprite and liquid mask).</summary>
	public ModItemVisual? Visual { get; set; }

	/// <summary>
	/// Crafting-quality labels the item provides, written into the vanilla
	/// <c>ItemInfo.qualities</c> so a quality-based recipe (vanilla or
	/// mod-authored) can match the item. A non-positive amount is normalised to
	/// <c>1</c>, like the liquid side, because the matcher asks for at least the
	/// amount a recipe requires.
	/// </summary>
	public List<ModCraftingQuality> Qualities
	{
		get;
		set => field = value ?? [];
	} = [];

}
