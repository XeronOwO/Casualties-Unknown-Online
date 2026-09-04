namespace CasualtiesUnknownOnline.GameState.Domains.Players;

/// <summary>
/// Kernel event carrying one authoritative cross-player inventory transfer
/// result. It is journal-only: the item ownership/state mutation is already
/// represented by item-domain events; the projection consumes this event to
/// restore the participant body mutation without a legacy direct wire message.
/// </summary>
public sealed record PlayerInventoryTransferEvent(
	ulong FromSteamId,
	ulong ToSteamId,
	PlayerInteractionItem Item,
	ulong TargetParentItemId = 0) : PlayerEvent;
