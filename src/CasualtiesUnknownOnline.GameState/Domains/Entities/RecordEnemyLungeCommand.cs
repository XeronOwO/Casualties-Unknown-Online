namespace CasualtiesUnknownOnline.GameState.Domains.Entities;

/// <summary>
/// Host-only command that records one crystal-lunge result in the kernel
/// journal. The victim's local body already applied the lunge; this command
/// carries the post-lunge limb/body facts so every participant projection
/// applies the same exact state.
/// </summary>
public sealed record RecordEnemyLungeCommand(
	OperationId OperationId,
	ActorId Actor,
	RunEpoch RunEpoch,
	AuthorityKind Authority,
	ulong VictimSteamId,
	EnemyCombatLimb Limb,
	float Adrenaline,
	float Stamina) : GameCommand(OperationId, Actor, RunEpoch, Authority, []);
