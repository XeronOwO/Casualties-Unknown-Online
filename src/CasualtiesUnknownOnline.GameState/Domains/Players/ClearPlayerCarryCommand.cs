namespace CasualtiesUnknownOnline.GameState.Domains.Players;

/// <summary>
/// Host-authorized command that clears a cross-player carry relation. When
/// <see cref="CarriedSteamId"/> is zero, the carrier's current relation is
/// cleared if any.
/// </summary>
public sealed record ClearPlayerCarryCommand(
	OperationId OperationId,
	ActorId Actor,
	RunEpoch RunEpoch,
	AuthorityKind Authority,
	ulong CarrierSteamId,
	ulong CarriedSteamId) : GameCommand(OperationId, Actor, RunEpoch, Authority, []);
