namespace CasualtiesUnknownOnline.GameState.Domains.Entities;

/// <summary>
/// Host-only command that records one enemy-proximity side-effect result in the
/// kernel journal. The affected player's local body already applied the effect;
/// this command carries the post-effect body facts so every participant
/// projection applies the same exact state.
/// </summary>
public sealed record RecordEnemyEffectCommand(
	OperationId OperationId,
	ActorId Actor,
	RunEpoch RunEpoch,
	AuthorityKind Authority,
	ulong VictimSteamId,
	EnemyCombatEffectKind Kind,
	float HorrifiedLevel,
	float FocusedLevel,
	float Adrenaline,
	float Energy,
	float Stamina,
	float Happiness,
	float Caffeinated,
	float SepticShock,
	float Shock,
	float EyePanicTime) : GameCommand(OperationId, Actor, RunEpoch, Authority, []);
