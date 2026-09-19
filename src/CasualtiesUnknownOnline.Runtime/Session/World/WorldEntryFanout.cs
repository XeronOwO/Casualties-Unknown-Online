using CasualtiesUnknownOnline.Runtime.Session.EntitySync;
using CasualtiesUnknownOnline.Runtime.Session.Items;
using Microsoft.Extensions.Logging;

namespace CasualtiesUnknownOnline.Runtime.Session.World;

/// <summary>
/// The world-entry backfill fan-out: owns the ordered snapshot group a member
/// receives on its first InWorld edge or on a reconnect while still InWorld.
/// Previously this method lived on <see cref="HandlerContext"/>, making that
/// class a god object that both handed handlers their control surfaces and
/// owned a concrete world-entry flow. This service is the single owner of that
/// fan-out and appends the explicit world-entry completion marker after the
/// snapshot group.
///
/// It also owns the IN-SESSION repair set (<see cref="SendInSessionRepair"/>):
/// the absolute in-world tables the host re-sends on its periodic cycle, so a
/// member that stays continuously in the world converges after a send the lazy
/// P2P session swallowed. Both flows are "what this member needs to be brought
/// to the host's state", so they live together — the trap layout was missing
/// from the periodic cycle for exactly as long as the two lists were owned in
/// different places.
///
/// Both groups carry the kernel checkpoint FIRST: it is what establishes the
/// member's run baseline, and every generation-stamped absolute table
/// (block state, block damage, trap layout, runtime entities) is compared
/// against that baseline on arrival — sending one before the checkpoint makes
/// the receiver refuse the host's own current-generation table (review finding,
/// cycle `review/generation-identity-remaining-families.md`).
/// </summary>
public sealed class WorldEntryFanout(
	IWorldControl world,
	IItemControl items,
	IEnemySyncControl enemies,
	IKernelProtocolControl kernelProtocol,
	ILogger<WorldEntryFanout> log)
{
	private readonly IWorldControl _world = world;
	private readonly IItemControl _items = items;
	private readonly IEnemySyncControl _enemies = enemies;
	private readonly IKernelProtocolControl _kernelProtocol = kernelProtocol;
	private readonly ILogger<WorldEntryFanout> _log = log;

	/// <summary>
	/// Host only: send the complete world-entry snapshot group to one member,
	/// then the explicit completion marker. Order-dependent by design — the
	/// receiver uses the marker to know the full backfill has arrived.
	/// </summary>
	public void Send(ulong steamId)
	{
		_log.LogInformation("Sending world-entry snapshot group to {Peer}.", steamId);

		// The kernel checkpoint goes FIRST because it is what carries the run
		// baseline (RunEpoch + LayerIndex) to this member, and every absolute
		// table below that is stamped with the world/layer generation is
		// compared against that baseline on arrival: a stamped table sent before
		// it would be refused as stale by the very member it is meant to bring
		// up to date, with no second chance before the 60 s repair.
		_kernelProtocol.SendCheckpoint(steamId);
		_world.SendBlockStateSnapshot(steamId);
		_world.SendBlockDamageSnapshot(steamId);
		_world.SendTrapLayoutSnapshot(steamId);
		_world.SendRadiationLineState(steamId);
		_items.SendItemSnapshot(steamId);
		_enemies.SendEnemySnapshot(steamId);
		_world.SendRuntimeEntitySnapshot(steamId);
		_world.SendWorldSnapshotComplete(steamId);
	}

	/// <summary>
	/// Host only: the in-session repair set for ONE member that stays in the
	/// world — the absolute, idempotent in-world tables, re-sent on the host's
	/// periodic cycle (the cadence and the in-world filter live in the adapter,
	/// which owns Unity time). Deliberately a SUBSET of <see cref="Send"/>: the
	/// item snapshot stays an entry-only one-shot (its rows are reconciled
	/// continuously by the item keyframe), the radiation line state is a scalar
	/// re-sent on its own generation/entry edges, and the completion marker is an
	/// entry-group contract.
	///
	/// The ENEMY snapshot rides here since the N1 recovery (2026-09-18): a
	/// repeat snapshot is idempotent because the guest pairs its frozen copies on
	/// the host's bind-time spawn anchor rather than on the live position, so a
	/// repair landing long after generation still binds (before the anchor it
	/// could not pair at all, and a failed pairing even cleared the guest's
	/// mapping flag).
	///
	/// Adding an absolute in-world table means adding it HERE as well: a
	/// swallowed entry send has no second chance until the next world-entry
	/// edge, which is the gap the trap-layout snapshot sat in (the sync-coverage
	/// audit's W6 row) and the enemy snapshot sat in (N1).
	/// </summary>
	public void SendInSessionRepair(ulong steamId)
	{
		_log.LogInformation("Sending the in-session repair group to {Peer}.", steamId);

		// Same rule as the entry group: the run-baseline carrier first, then the
		// generation-stamped absolute tables it is compared against.
		_kernelProtocol.SendCheckpoint(steamId);
		_world.SendBlockStateSnapshot(steamId);
		_world.SendBlockDamageSnapshot(steamId);
		_world.SendTrapLayoutSnapshot(steamId);
		_enemies.SendEnemySnapshot(steamId);
		_world.SendRuntimeEntitySnapshot(steamId);
	}
}
