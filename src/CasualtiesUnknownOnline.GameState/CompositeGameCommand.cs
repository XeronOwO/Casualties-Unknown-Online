using System.Collections.Generic;

namespace CasualtiesUnknownOnline.GameState;

/// <summary>
/// A host-only composite command that atomically executes several typed domain
/// commands as one kernel batch. If any inner command is rejected the whole
/// composite is rejected; otherwise all emitted events are reduced under one
/// global revision.
/// </summary>
public sealed record CompositeGameCommand(
	OperationId OperationId,
	ActorId Actor,
	RunEpoch RunEpoch,
	AuthorityKind Authority,
	IReadOnlyList<GameCommand> Commands) : GameCommand(OperationId, Actor, RunEpoch, Authority, []);
