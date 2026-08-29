namespace CasualtiesUnknownOnline.GameState.Domains.Fluids;

/// <summary>
/// Host-only command that upserts one persistent fluid-region checkpoint.
/// </summary>
public sealed record UpdateFluidRegionCommand(
	OperationId OperationId,
	ActorId Actor,
	RunEpoch RunEpoch,
	AuthorityKind Authority,
	FluidRegionState State) : GameCommand(OperationId, Actor, RunEpoch, Authority, []);
