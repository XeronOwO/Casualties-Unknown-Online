using System.Collections.Generic;

namespace CasualtiesUnknownOnline.GameState.Domains.Players;

/// <summary>
/// Host-only command that records one cross-player consumable/wearable use
/// result in the kernel journal. The durable item ownership/state and player
/// terminal mutations already ride their respective domains; this command
/// carries the post-use item/wearable and body snapshots so the user and target
/// projections apply the exact host-authoritative result.
/// </summary>
public sealed record RecordPlayerItemUseResultCommand(
	OperationId OperationId,
	ActorId Actor,
	RunEpoch RunEpoch,
	AuthorityKind Authority,
	ulong UserSteamId,
	ulong TargetSteamId,
	ulong ItemInstanceId,
	bool ItemDestroyed,
	PlayerInteractionItem? ItemAfter,
	PlayerInteractionItem? WornItem,
	PlayerInteractionHealth? Health,
	IReadOnlyList<PlayerInteractionLimb> Limbs,
	IReadOnlyList<PlayerInteractionTimedLimbEffect> TimedEffects,
	IReadOnlyList<PlayerInteractionTimedBodyEffect> TimedBodyEffects) : GameCommand(OperationId, Actor, RunEpoch, Authority, []);
