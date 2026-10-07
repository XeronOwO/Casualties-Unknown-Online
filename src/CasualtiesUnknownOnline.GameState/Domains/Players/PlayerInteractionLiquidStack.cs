namespace CasualtiesUnknownOnline.GameState.Domains.Players;

/// <summary>
/// Kernel-shaped liquid stack carried by a cross-player item-use result: one
/// entry of the drain the host committed for a migrated topical use. It is
/// effect data, not a terminal fact — the item's own drained state rides
/// <c>PlayerInteractionItem</c>, and the target's local body is the side that
/// turns this into a native <c>onHealthUse</c> call.
/// </summary>
public sealed record PlayerInteractionLiquidStack(
	string LiquidId,
	float Amount);
