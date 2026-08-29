using System.Collections.Generic;
using CasualtiesUnknownOnline.GameState.Domains.Items;

namespace CasualtiesUnknownOnline.GameState.Domains.Players;

/// <summary>
/// Kernel-shaped snapshot of one item carried in a cross-player interaction
/// result. It carries the same identity/payload facts the item domain owns,
/// without the aggregate revision/location fields that are not part of the
/// result event. Container contents are recursive because the participant
/// projection must restore the exact local item tree in one result.
/// </summary>
public sealed record PlayerInteractionItem(
	ItemIdentity Identity,
	ItemData Data,
	IReadOnlyList<PlayerInteractionItem>? Contents = null)
{
	public IReadOnlyList<PlayerInteractionItem> Children => Contents ?? [];
}

