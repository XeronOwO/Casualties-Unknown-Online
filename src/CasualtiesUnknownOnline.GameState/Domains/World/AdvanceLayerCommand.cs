namespace CasualtiesUnknownOnline.GameState.Domains.World;

/// <summary>
/// Host-only command that replaces the run baseline when the world advances to
/// a new layer. This keeps Random.state and the world-defining fields under the
/// same authoritative domain as the run identity.
/// </summary>
public sealed record AdvanceLayerCommand(
	OperationId OperationId,
	ActorId Actor,
	RunEpoch RunEpoch,
	AuthorityKind Authority,
	RunState Run) : GameCommand(OperationId, Actor, RunEpoch, Authority, []);
