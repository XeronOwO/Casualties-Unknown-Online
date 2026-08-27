using System.Collections.Generic;

namespace CasualtiesUnknownOnline.GameState;

/// <summary>
/// Base type for every kernel command. Commands are typed requests; they may be
/// rejected and they never mutate kernel state directly.
/// </summary>
public abstract record GameCommand(
	OperationId OperationId,
	ActorId Actor,
	RunEpoch RunEpoch,
	AuthorityKind Authority,
	IReadOnlyList<ExpectedRevision> Preconditions);
