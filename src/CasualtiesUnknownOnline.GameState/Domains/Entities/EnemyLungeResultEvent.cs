namespace CasualtiesUnknownOnline.GameState.Domains.Entities;

/// <summary>
/// Kernel event carrying one authoritative crystal-lunge result. It is
/// journal-only: the victim's body mutation is a local-compute fact; the
/// projection consumes this event to restore the post-lunge presentation state
/// without a legacy direct result wire.
/// </summary>
public sealed record EnemyLungeResultEvent(
	ulong VictimSteamId,
	EnemyCombatLimb Limb,
	float Adrenaline,
	float Stamina) : EnemyEvent;
