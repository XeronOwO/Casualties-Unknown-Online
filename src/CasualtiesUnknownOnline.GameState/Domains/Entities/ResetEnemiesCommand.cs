namespace CasualtiesUnknownOnline.GameState.Domains.Entities;

/// <summary>
/// Host-only command that clears all enemy/entity facts for a new run/layer.
/// </summary>
public sealed record ResetEnemiesCommand(
	OperationId OperationId,
	ActorId Actor,
	RunEpoch RunEpoch,
	AuthorityKind Authority) : GameCommand(OperationId, Actor, RunEpoch, Authority, []);
