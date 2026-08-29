namespace CasualtiesUnknownOnline.GameState.Domains.Players;

/// <summary>
/// Host-authorized command that records one cross-player carry relation: the
/// carrier is carrying the carried player.
/// </summary>
public sealed record SetPlayerCarryCommand(
	OperationId OperationId,
	ActorId Actor,
	RunEpoch RunEpoch,
	AuthorityKind Authority,
	ulong CarrierSteamId,
	ulong CarriedSteamId) : GameCommand(OperationId, Actor, RunEpoch, Authority, []);
