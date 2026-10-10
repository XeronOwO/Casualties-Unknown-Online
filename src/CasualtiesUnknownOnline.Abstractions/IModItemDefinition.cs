using System.Collections.Generic;

namespace CasualtiesUnknownOnline.Abstractions;

/// <summary>
/// One item declaration. This is the contract a consumer reads: the Game
/// Adapter's item provider materializes an <see cref="Kind"/>-checked
/// <see cref="ModContentKind.Item"/> entry from these members instead of from a
/// concrete class, so a mod that computes a value declares its own type instead
/// of filling in the framework's.
///
/// <see cref="ModItemDefinition"/> is the framework's ready-made implementation
/// of this interface: use it when every value is a constant, and implement the
/// interface when one is computed. Partial customisation is composition — an
/// implementation hands back a filled default for the members it does not touch
/// — never inheritance from the data class, which stays <c>sealed</c>.
/// </summary>
public interface IModItemDefinition : IModContentDefinition
{
	/// <summary>Player-facing item name.</summary>
	string DisplayName { get; }

	/// <summary>Player-facing item description.</summary>
	string Description { get; }

	/// <summary>Vanilla spawn/category tag; defaults to "nospawn" when empty.</summary>
	string Category { get; }

	/// <summary>Item weight in vanilla units.</summary>
	float Weight { get; }

	/// <summary>Vanilla item value.</summary>
	int Value { get; }

	/// <summary>Whether the item can be used from the hand.</summary>
	bool Usable { get; }

	/// <summary>Whether the item can be used with the left mouse button.</summary>
	bool UsableWithLmb { get; }

	/// <summary>Whether the item is destroyed when its condition reaches zero.</summary>
	bool DestroyAtZeroCondition { get; }

	/// <summary>Vanilla tag string, empty when none.</summary>
	string Tags { get; }

	/// <summary>Relative spawn/trader/loot weighting.</summary>
	int SpawnFrequency { get; }

	/// <summary>
	/// The vanilla prefab id used as the runtime template base. Empty means the
	/// definition is static-item-info only; the Game Adapter cannot materialize
	/// a prefab for it.
	/// </summary>
	string TemplateId { get; }

	/// <summary>
	/// Component type names (assembly-qualified or simple names) attached to the
	/// runtime template before it is instantiated. The Game Adapter resolves
	/// the types from loaded assemblies and refuses non-Component types. Null
	/// means none.
	/// </summary>
	List<string> SpawnComponents { get; }

	/// <summary>Extensible mod-owned metadata for future binders/features.</summary>
	Dictionary<string, string> CustomData { get; }

	/// <summary>
	/// Average loose world-spawn count per worldgen chunk. Null or zero disables
	/// automatic world spawning; a positive value makes the Game Adapter scatter
	/// the item on ground inside the isolated generation stream. The existing
	/// generation-item snapshot synchronizes both sides — no new wire is needed.
	/// </summary>
	float? WorldSpawnPerChunk { get; }

	/// <summary>
	/// Optional explicit fixed drop-source pools. When set, the item is not added
	/// to the generic vanilla category loot pool; instead it is registered only in
	/// the selected source pools (corpse, built-in crates, trader stock). Leave
	/// null to use the vanilla category fallback.
	/// </summary>
	ModItemDropSource? DropSources { get; }

	/// <summary>Optional container behavior applied to the runtime item template.</summary>
	IModItemContainer? Container { get; }

	/// <summary>Optional battery behavior applied to the runtime item template.</summary>
	IModItemBattery? Battery { get; }

	/// <summary>Optional light behavior applied to the runtime item template.</summary>
	IModItemLight? Light { get; }

	/// <summary>Optional melee/tool behavior applied to the item's static use action.</summary>
	IModItemTool? Tool { get; }

	/// <summary>Optional firearm behavior applied to the runtime item template and static use action.</summary>
	IModItemGun? Gun { get; }

	/// <summary>
	/// Optional wearable behavior: where the item is worn and what the game's own
	/// wear flow reads once it is there. Null — and equally a declaration naming no
	/// limb or no slot — means the item is not worn: the game's placement resolves
	/// the limb NAME the declaration carries and dereferences the result, so a flag
	/// without a resolvable limb would throw inside the game's own flow. An
	/// incomplete declaration is registered without the flag and reported at load.
	/// </summary>
	IModItemWearable? Wearable { get; }

	/// <summary>
	/// Vanilla decay time in in-game minutes. Zero disables time-based decay;
	/// a positive value also sets the computed <c>rotSpeed</c> used by the
	/// vanilla decay path (including battery-powered drain when
	/// <see cref="Battery"/> is present).
	/// </summary>
	float DecayMinutes { get; }

	/// <summary>Optional visual presentation (worn sprite and liquid mask).</summary>
	IModItemVisual? Visual { get; }

	/// <summary>
	/// Crafting-quality labels the item provides, written into the vanilla
	/// <c>ItemInfo.qualities</c> so a quality-based recipe (vanilla or
	/// mod-authored) can match the item. A non-positive amount is normalised to
	/// <c>1</c>, like the liquid side, because the matcher asks for at least the
	/// amount a recipe requires.
	/// </summary>
	List<IModCraftingQuality> Qualities { get; }
}
