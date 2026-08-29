namespace CasualtiesUnknownOnline.GameState.Domains.Players;

/// <summary>
/// Host-only command that clears all player terminal facts for a new run.
/// </summary>
public sealed record ResetPlayersCommand(
	OperationId OperationId,
	ActorId Actor,
	RunEpoch RunEpoch,
	AuthorityKind Authority) : GameCommand(OperationId, Actor, RunEpoch, Authority, []);
