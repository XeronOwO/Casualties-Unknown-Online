namespace CasualtiesUnknownOnline.GameState.Domains.Entities;

/// <summary>
/// Kernel event carrying one authoritative enemy-proximity side-effect result.
/// It is journal-only: the affected player's body mutation is a local-compute
/// fact; the projection consumes this event to restore the post-effect
/// presentation state without a legacy direct result wire.
/// </summary>
public sealed record EnemyEffectResultEvent(
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
	float EyePanicTime) : EnemyEvent;
