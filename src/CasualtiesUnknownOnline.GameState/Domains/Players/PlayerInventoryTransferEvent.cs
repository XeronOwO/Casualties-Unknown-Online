namespace CasualtiesUnknownOnline.GameState.Domains.Players;

/// <summary>
/// Kernel event carrying one authoritative cross-player inventory transfer
/// result. It is journal-only: the item ownership/state mutation is already
/// represented by item-domain events; the projection consumes this event to
/// restore the participant body mutation without a legacy direct wire message.
/// <see cref="Destination"/> is the ladder rung the host chose: the receiving
/// client applies THAT rung (a body slot, a container's contents, or a world
/// drop) instead of choosing one of its own. <see cref="ExactDestination"/> marks the destination as the
/// ANSWERED one (the give's receiver resolved it), so the receiving client may not substitute another slot or
/// container: it reports the failure and the host returns the row.
/// </summary>
public sealed record PlayerInventoryTransferEvent(
	ulong FromSteamId,
	ulong ToSteamId,
	PlayerInteractionItem Item,
	ulong TargetParentItemId = 0,
	PlayerInventoryDestination Destination = PlayerInventoryDestination.BodySlot,
	bool ExactDestination = false,
	bool Returned = false) : PlayerEvent;
