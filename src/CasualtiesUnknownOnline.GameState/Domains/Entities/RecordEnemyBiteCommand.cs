namespace CasualtiesUnknownOnline.GameState.Domains.Entities;

/// <summary>
/// Host-only command that records one enemy-bite result in the kernel journal.
/// The victim's local body already applied the bite; this command carries the
/// post-bite limb/body facts so every participant projection applies the same
/// exact state.
/// </summary>
public sealed record RecordEnemyBiteCommand(
	OperationId OperationId,
	ActorId Actor,
	RunEpoch RunEpoch,
	AuthorityKind Authority,
	ulong VictimSteamId,
	EnemyCombatLimb Limb,
	float VenomTotal,
	float Adrenaline,
	float Happiness) : GameCommand(OperationId, Actor, RunEpoch, Authority, []);
