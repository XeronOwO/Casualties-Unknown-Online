namespace CasualtiesUnknownOnline.GameState.Domains.Entities;

/// <summary>
/// Host-only command that upserts one enemy/entity fact.
/// </summary>
public sealed record UpsertEnemyCommand(
	OperationId OperationId,
	ActorId Actor,
	RunEpoch RunEpoch,
	AuthorityKind Authority,
	EnemyState State) : GameCommand(OperationId, Actor, RunEpoch, Authority, []);
