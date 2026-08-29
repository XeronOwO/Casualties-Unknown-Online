using System.Collections.Generic;

namespace CasualtiesUnknownOnline.GameState.Domains.Players;

/// <summary>
/// Host-only command that records one cross-player heal result in the kernel
/// journal. The durable item/player terminal mutations already ride their
/// respective domains; this command carries the post-heal body snapshot so the
/// target and healer projections apply the exact host-authoritative result.
/// </summary>
public sealed record RecordPlayerHealResultCommand(
	OperationId OperationId,
	ActorId Actor,
	RunEpoch RunEpoch,
	AuthorityKind Authority,
	ulong HealerSteamId,
	ulong TargetSteamId,
	ulong ItemInstanceId,
	bool ItemDestroyed,
	float ItemConditionAfter,
	int HealedLimbIndex,
	PlayerInteractionHealth? Health,
	IReadOnlyList<PlayerInteractionLimb> Limbs) : GameCommand(OperationId, Actor, RunEpoch, Authority, []);
