namespace CasualtiesUnknownOnline.GameState.Domains.WorldEntities;

/// <summary>
/// Host-only command that records a one-shot trap/mechanism consumption at a
/// world-cell position.
/// </summary>
public sealed record RecordTrapConsumedCommand(
	OperationId OperationId,
	ActorId Actor,
	RunEpoch RunEpoch,
	AuthorityKind Authority,
	EntityPosition Position,
	int Kind,
	byte Extra,
	long TriggeredAtMs) : GameCommand(OperationId, Actor, RunEpoch, Authority, []);
