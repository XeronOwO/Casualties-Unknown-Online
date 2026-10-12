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

	/// <summary>
	/// Whether the item can be applied to a limb of the local player — the flag the
	/// game's own wound view gates the item's limb action on
	/// (<c>ItemInfo.usableOnLimb</c>, read through <c>ItemInfo.ActuallyUsableOnLimb</c>
	/// for an item that is not a liquid container). A definition that declares it
	/// without registering a limb action through <see cref="IModItemActions"/> is
	/// well-defined rather than a crash — the framework installs a reporting no-op
	/// for it, the same outcome the use half settled — and the game will offer the
	/// item on a limb either way.
	/// </summary>
	bool UsableOnLimb { get; }

	/// <summary>Whether the item is destroyed when its condition reaches zero.</summary>
	bool DestroyAtZeroCondition { get; }

	/// <summary>
	/// The INT level a player needs before the game shows the item's own name —
	/// the gate <c>Recognition.recognizable</c> answers with
	/// <c>min &lt;= PlayerCamera.main.body.skills.INT</c> (<c>Recognition.cs:14-19</c>) and
	/// which <c>PlayerCamera.ItemHoverDescription</c> (<c>PlayerCamera.cs:1043-1055</c>)
	/// reads to replace the name with <c>unknownobject</c> (or <c>unknown&lt;category&gt;</c>),
	/// replace the description with <c>unknownobjectdsc</c> and print this number in
	/// the warning line it appends. The same gate names the item in the player's own
	/// hands in the HUD (<c>PlayerCamera.cs:2126</c>, <c>:2139</c>), in a minigame's
	/// item text (<c>MinigameBase.cs:223</c>) and in the two wear alerts
	/// (<c>Body.cs:1487</c>, <c>:1496</c>).
	///
	/// <para>
	/// Zero — the default — means every player recognizes the item, which is what a
	/// definition that wrote its own name and description asked for. A positive value
	/// opts into the game's own mechanic; the number the tooltip prints is this one.
	/// A negative value is refused at registration.
	/// </para>
	/// </summary>
	int RecognitionMinimum { get; }

	/// <summary>
	/// Whether every slot that is not a hand refuses the item —
	/// <c>ItemInfo.onlyHoldInHands</c>, read by the slot pickup
	/// (<c>Body.cs:1390</c>), the inventory drag (<c>PlayerCamera.cs:1172</c>) and the
	/// slot button's own colour (<c>InvButton.cs:137</c>).
	/// </summary>
	bool OnlyHoldInHands { get; }

	/// <summary>
	/// Whether two of the item can be merged into one — <c>ItemInfo.combineable</c>,
	/// read by <c>Body.CanCombine</c> (<c>Body.cs:1297</c>), whose rule requires BOTH
	/// operands to set it.
	/// </summary>
	bool Combineable { get; }

	/// <summary>
	/// Whether the item stays usable while its holder is below the medical cutoff —
	/// <c>ItemInfo.ignoreDepression</c>, read by the use gate
	/// (<c>PlayerCamera.cs:745</c>) and by the inventory button's lock
	/// (<c>InvButton.cs:85</c>).
	/// </summary>
	bool IgnoreDepression { get; }

	/// <summary>
	/// Whether the weight this item contributes to its carrier follows its remaining
	/// condition — <c>ItemInfo.scaleWeightWithCondition</c>, which the framework's own
	/// encumbrance calculation reads
	/// (<c>CarriedEncumbranceCalculator</c>: <c>Mathf.Lerp(0.1f, weight, condition)</c>
	/// when it is set, the flat weight when it is not).
	/// </summary>
	bool ScaleWeightWithCondition { get; }

	/// <summary>
	/// Whether holding the attack button repeats the item's use —
	/// <c>ItemInfo.autoAttack</c>, read by the held-button loop
	/// (<c>PlayerCamera.cs:1831</c>, <c>:1836</c>). Declaring <see cref="Tool"/> or
	/// <see cref="Gun"/> sets it already; this member is how an item whose behaviour is
	/// its own <see cref="IModItemActions"/> action asks for the same.
	/// </summary>
	bool AutoAttack { get; }

	/// <summary>
	/// The change a held item makes to its carrier's jump height —
	/// <c>ItemInfo.jumpHeightMultChange</c>, applied as <c>height *= value + 1</c>
	/// (<c>Body.cs:2120-2122</c>). Zero — the default — means no change, and the game's
	/// own two users declare <c>0.44f</c> (a booster) and <c>-0.1f</c> (a penalty). A
	/// value at or below <c>-1</c> is refused at registration: the factor would be zero
	/// or negative.
	/// </summary>
	float JumpHeightMultiplierChange { get; }

	/// <summary>
	/// The rotation a held item is drawn at, in degrees — <c>ItemInfo.slotRotation</c>,
	/// written onto the held object's <c>localEulerAngles.z</c> (<c>Body.cs:1403</c>) and
	/// read by the framework's own clone renderer (<c>CloneInventoryRenderer</c>).
	/// </summary>
	float SlotRotation { get; }

	/// <summary>
	/// The item's vanilla tags, comma-separated — the string the game's own
	/// <c>ItemInfo.SetTags</c> splits into the array <c>ItemInfo.HasTag</c> queries.
	/// Empty — the default — is a legal declaration meaning "carries no tag": the
	/// derived array still exists (holding the one empty entry the split of an empty
	/// string produces), so the game's own <c>HasTag</c> answers <c>false</c> for
	/// every tag a caller asks for instead of throwing on a null array. Batch
	/// <c>20261011-k</c> measured the alternative: a tagless declaration used to
	/// throw <c>ArgumentNullException</c> inside the game's <c>Item.Start</c> once per
	/// item, and once per frame through <c>PlayerCamera.HandleGunMenu</c> while the
	/// item sat in the hand slot.
	/// </summary>
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

	/// <summary>
	/// The conditions under which an item with a positive <see cref="DecayMinutes"/>
	/// decays at all — the game's own decay flags (<c>Item.HandleDecay</c>,
	/// <c>Item.cs:186-197</c>). <see cref="ModItemDecayCondition.None"/> — the
	/// default — means it decays wherever it is, which is also what every item
	/// declared before this member did. Conditions with no positive
	/// <see cref="DecayMinutes"/> gate a decay that never starts: the declaration is
	/// registered and reported at load rather than refused.
	/// </summary>
	ModItemDecayCondition DecayConditions { get; }

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
