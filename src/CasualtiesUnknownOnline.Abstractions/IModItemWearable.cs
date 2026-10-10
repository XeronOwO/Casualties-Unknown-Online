namespace CasualtiesUnknownOnline.Abstractions;

/// <summary>
/// How and where a custom item is worn. The values are plain data in
/// Abstractions; the Game Adapter writes them into the vanilla <c>ItemInfo</c>
/// fields the game's own wear flow reads to place the garment.
///
/// <para>
/// <see cref="Limb"/> and <see cref="SlotId"/> together are the placement, and
/// they are required for the item to be wearable at all: the game's own
/// <c>Body.WearWearable</c> resolves the declared limb name with
/// <c>Body.LimbByName</c> and dereferences the result on the next line, so a
/// declaration naming no limb is not a garment the game can place. A definition
/// whose placement is incomplete is registered WITHOUT the wearable flag and
/// reported at load rather than handed to a call that would throw.
/// </para>
/// </summary>
public interface IModItemWearable
{
	/// <summary>
	/// Vanilla limb name the garment is worn on — the name the game's own
	/// <c>Body.LimbByName</c> lookup resolves, compared exactly as the game
	/// compares it (e.g. <c>Head</c>, <c>LeftArm</c>). Empty means the item
	/// cannot be worn.
	/// </summary>
	string Limb { get; }

	/// <summary>
	/// Wear slot the garment occupies — what the game's own occupancy check
	/// compares and what its save file records, so two garments in one slot
	/// collide even when they name different limbs. Empty means the item cannot
	/// be worn.
	/// </summary>
	string SlotId { get; }

	/// <summary>
	/// Whether the garment may be carried in the inventory instead of only worn
	/// (vanilla <c>wearableCanBeHeld</c>): false keeps the game's own refusal to
	/// drag it into a slot.
	/// </summary>
	bool CanBeHeld { get; }

	/// <summary>Protection the garment adds on the limb it covers (vanilla <c>wearableArmor</c>).</summary>
	float Armor { get; }

	/// <summary>Cold protection the garment gives while worn (vanilla <c>wearableIsolation</c>).</summary>
	float Isolation { get; }

	/// <summary>Multiplier on the condition the garment loses when the covered limb takes a hit (vanilla <c>wearableHitDurabilityLossMultiplier</c>).</summary>
	float HitDurabilityLossMultiplier { get; }

	/// <summary>Sorting-order offset of the worn sprite against the limb it hangs on (vanilla <c>wearableVisualOffset</c>, whose own default is 5).</summary>
	int VisualOffset { get; }
}
