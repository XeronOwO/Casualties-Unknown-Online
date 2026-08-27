using System.Collections.Generic;
using System.Linq;

namespace CasualtiesUnknownOnline.GameState.Domains.Items;

/// <summary>
/// The kernel-owned persistent state of one item, independent of any wire DTO.
/// It covers the save-shaped item facts that must travel with identity and
/// location: condition, favourited flag, the owner-local slot, liquid stacks,
/// and the typed component-state payload. Container contents are not embedded
/// here; each contained item is its own <see cref="ItemState"/> with a
/// Contained location so the container graph is authoritative and acyclic.
/// </summary>
public readonly record struct ItemData(
	float Condition,
	bool Favourited,
	int SlotIndex,
	IReadOnlyList<ItemLiquidStack> Liquids,
	IReadOnlyList<ItemComponentState> Components)
{
	/// <summary>Neutral empty state for an item that carries no save-shaped payload.</summary>
	public static ItemData Empty { get; } = new(
		0f,
		false,
		-1,
		[],
		[]);

	public bool IsEmpty =>
		Condition == 0f
		&& !Favourited
		&& SlotIndex == -1
		&& Liquids.Count == 0
		&& Components.Count == 0;

	/// <summary>
	/// Structural equality helper used by checkpoint and projection tests. The
	/// record's default equality on list interfaces is reference-based, so this
	/// method compares the serialized payload contents instead.
	/// </summary>
	public bool SemanticallyEquals(ItemData other) =>
		Condition == other.Condition
		&& Favourited == other.Favourited
		&& SlotIndex == other.SlotIndex
		&& Liquids.Count == other.Liquids.Count
		&& Liquids.SequenceEqual(other.Liquids)
		&& Components.Count == other.Components.Count
		&& Components.SequenceEqual(other.Components);
}
