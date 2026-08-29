namespace CasualtiesUnknownOnline.GameState.Domains.Entities;

/// <summary>
/// Host-only command that removes one enemy/entity fact.
/// </summary>
public sealed record RemoveEnemyCommand(
	OperationId OperationId,
	ActorId Actor,
	RunEpoch RunEpoch,
	AuthorityKind Authority,
	EntityId EntityId) : GameCommand(OperationId, Actor, RunEpoch, Authority, []);
