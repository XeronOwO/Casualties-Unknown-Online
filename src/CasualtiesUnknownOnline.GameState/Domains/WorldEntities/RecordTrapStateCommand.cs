namespace CasualtiesUnknownOnline.GameState.Domains.WorldEntities;

/// <summary>
/// Host-only command that records one observed trap state transition at a world
/// position/kind. The kernel owns the legal transition table; the adapter keeps
/// the native state machine driver.
/// </summary>
public sealed record RecordTrapStateCommand(
	OperationId OperationId,
	ActorId Actor,
	RunEpoch RunEpoch,
	AuthorityKind Authority,
	EntityPosition Position,
	int Kind,
	TrapPhase Phase,
	byte Extra,
	long TransitionedAtMs) : GameCommand(OperationId, Actor, RunEpoch, Authority, []);
