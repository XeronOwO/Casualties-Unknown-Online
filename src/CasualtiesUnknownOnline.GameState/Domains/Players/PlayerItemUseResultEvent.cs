using System.Collections.Generic;

namespace CasualtiesUnknownOnline.GameState.Domains.Players;

/// <summary>
/// Kernel event carrying one authoritative cross-player consumable/wearable use
/// result. It is journal-only: the consumed/transferred item and player terminal
/// facts already ride their own domain events; the projection consumes this
/// event to restore the user/target body mutation without a legacy direct wire
/// message.
/// </summary>
public sealed record PlayerItemUseResultEvent(
	ulong UserSteamId,
	ulong TargetSteamId,
	ulong ItemInstanceId,
	bool ItemDestroyed,
	PlayerInteractionItem? ItemAfter,
	PlayerInteractionItem? WornItem,
	PlayerInteractionHealth? Health,
	IReadOnlyList<PlayerInteractionLimb> Limbs,
	IReadOnlyList<PlayerInteractionTimedLimbEffect> TimedEffects,
	IReadOnlyList<PlayerInteractionTimedBodyEffect> TimedBodyEffects) : PlayerEvent;
