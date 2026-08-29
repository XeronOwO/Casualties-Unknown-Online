namespace CasualtiesUnknownOnline.GameState.Domains.WorldEntities;

/// <summary>
/// Host-only command that clears all world-entity facts for a new world/layer.
/// The RunState itself is not touched; only the position-keyed facts are reset.
/// </summary>
public sealed record ResetWorldEntitiesCommand(
	OperationId OperationId,
	ActorId Actor,
	RunEpoch RunEpoch,
	AuthorityKind Authority) : GameCommand(OperationId, Actor, RunEpoch, Authority, []);
