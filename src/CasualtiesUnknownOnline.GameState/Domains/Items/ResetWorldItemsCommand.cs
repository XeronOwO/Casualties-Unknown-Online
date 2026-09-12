namespace CasualtiesUnknownOnline.GameState.Domains.Items;

/// <summary>
/// Host-only command that clears every WORLD-ROOTED item for a new world/layer:
/// the previous layer's items are gone with the old scene, so the authoritative
/// kernel must not keep them (otherwise they accumulate across layers and a
/// later cut archives the dead layers' items). Carried items — and everything
/// inside them — travel with the player across the boundary and are left alone,
/// as are terminal records (a destroyed item is never resurrected).
/// </summary>
public sealed record ResetWorldItemsCommand(
	OperationId OperationId,
	ActorId Actor,
	RunEpoch RunEpoch,
	AuthorityKind Authority) : GameCommand(OperationId, Actor, RunEpoch, Authority, []);
