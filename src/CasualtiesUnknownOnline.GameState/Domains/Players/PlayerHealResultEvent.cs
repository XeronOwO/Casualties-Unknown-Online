using System.Collections.Generic;

namespace CasualtiesUnknownOnline.GameState.Domains.Players;

/// <summary>
/// Kernel event carrying one authoritative cross-player heal result. It is
/// journal-only: the consumed/updated item and player terminal facts already
/// ride their own domain events; the projection consumes this event to restore
/// the healer/target body mutation without a legacy direct wire message.
/// </summary>
public sealed record PlayerHealResultEvent(
	ulong HealerSteamId,
	ulong TargetSteamId,
	ulong ItemInstanceId,
	bool ItemDestroyed,
	float ItemConditionAfter,
	int HealedLimbIndex,
	PlayerInteractionHealth? Health,
	IReadOnlyList<PlayerInteractionLimb> Limbs) : PlayerEvent;
