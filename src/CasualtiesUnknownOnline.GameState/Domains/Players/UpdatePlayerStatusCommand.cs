namespace CasualtiesUnknownOnline.GameState.Domains.Players;

/// <summary>
/// Host-authorized command that upserts one player's terminal status.
/// </summary>
public sealed record UpdatePlayerStatusCommand(
	OperationId OperationId,
	ActorId Actor,
	RunEpoch RunEpoch,
	AuthorityKind Authority,
	PlayerState State) : GameCommand(OperationId, Actor, RunEpoch, Authority, []);
