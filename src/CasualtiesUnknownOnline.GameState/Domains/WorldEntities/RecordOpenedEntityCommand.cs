namespace CasualtiesUnknownOnline.GameState.Domains.WorldEntities;

/// <summary>
/// Host-only command that records an opened lockable entity at a world-cell
/// position.
/// </summary>
public sealed record RecordOpenedEntityCommand(
	OperationId OperationId,
	ActorId Actor,
	RunEpoch RunEpoch,
	AuthorityKind Authority,
	EntityPosition Position) : GameCommand(OperationId, Actor, RunEpoch, Authority, []);
