namespace CasualtiesUnknownOnline.GameState.Domains.WorldEntities;

/// <summary>
/// Host-only command that records a building entity's current health at a
/// world-cell position.
/// </summary>
public sealed record RecordBuildingEntityHealthCommand(
	OperationId OperationId,
	ActorId Actor,
	RunEpoch RunEpoch,
	AuthorityKind Authority,
	EntityPosition Position,
	float Health) : GameCommand(OperationId, Actor, RunEpoch, Authority, []);
