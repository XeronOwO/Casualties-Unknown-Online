namespace CasualtiesUnknownOnline.GameState.Domains.World;

/// <summary>
/// Host-only command that starts a new run inside the current kernel epoch.
/// The run baseline is the complete capture (Random.state, run settings, world
/// fields) taken at the host's run-start entry.
/// </summary>
public sealed record StartRunCommand(
	OperationId OperationId,
	ActorId Actor,
	RunEpoch RunEpoch,
	AuthorityKind Authority,
	RunState Run) : GameCommand(OperationId, Actor, RunEpoch, Authority, []);
