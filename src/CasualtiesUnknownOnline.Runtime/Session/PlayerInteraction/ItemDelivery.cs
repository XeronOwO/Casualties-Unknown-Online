using CasualtiesUnknownOnline.GameState.Domains.Players;

namespace CasualtiesUnknownOnline.Runtime.Session.PlayerInteraction;

/// <summary>
/// What one created-item delivery did: the ladder rung that took it and where on that rung it landed.
/// The give answer is built from this, so the sentence the operator reads names the rung that really
/// settled the item — a body slot, a container (with its definition and id), or the world drop.
/// </summary>
internal readonly record struct ItemDelivery(
	bool Delivered,
	string? Refusal,
	PlayerInventoryDestination Destination,
	int SlotIndex,
	ulong ContainerId,
	string? ContainerDefinitionId)
{
	/// <summary>A delivery that never happened; the sentence is the refusal the operator reads.</summary>
	internal static ItemDelivery Refused(string refusal) =>
		new(false, refusal, PlayerInventoryDestination.BodySlot, -1, 0, null);
}
