namespace CasualtiesUnknownOnline.GameState.Domains.Fluids;

/// <summary>
/// Host-only command that clears all fluid-region facts for a new run.
/// </summary>
public sealed record ResetFluidsCommand(
	OperationId OperationId,
	ActorId Actor,
	RunEpoch RunEpoch,
	AuthorityKind Authority) : GameCommand(OperationId, Actor, RunEpoch, Authority, []);
