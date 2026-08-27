using System.Collections.Generic;

namespace CasualtiesUnknownOnline.GameState;

/// <summary>
/// One atomic set of accepted facts with global ordering. Batches are the only
/// way confirmed state changes; effects are derived by projections, not stored
/// here.
/// </summary>
public sealed record CommittedBatch(
	OperationId OperationId,
	ulong GlobalRevision,
	ActorId Actor,
	AuthorityKind Authority,
	RunEpoch RunEpoch,
	IReadOnlyList<ExpectedRevision> Preconditions,
	IReadOnlyList<GameEvent> Events);
