namespace CasualtiesUnknownOnline.GameState.Domains.Entities;

/// <summary>
/// Kernel event carrying one authoritative enemy-bite result. It is
/// journal-only: the victim's body mutation is a local-compute fact and the
/// players/items domains do not need a new table entry; the projection consumes
/// this event to restore the post-bite presentation state without a legacy
/// direct result wire.
/// </summary>
public sealed record EnemyBiteResultEvent(
	ulong VictimSteamId,
	EnemyCombatLimb Limb,
	float VenomTotal,
	float Adrenaline,
	float Happiness) : EnemyEvent;
