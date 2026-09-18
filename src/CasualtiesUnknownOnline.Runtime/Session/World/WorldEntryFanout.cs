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
		_world.SendBlockStateSnapshot(steamId);
		_world.SendBlockDamageSnapshot(steamId);
		_world.SendTrapLayoutSnapshot(steamId);
		_world.SendRadiationLineState(steamId);
		_kernelProtocol.SendCheckpoint(steamId);
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
	/// ordered entry group's other members are either heavy one-shots (the item
	/// snapshot, the enemy snapshot) or a scalar re-sent on its own
	/// generation/entry edges (the radiation line state), and the completion
	/// marker is an entry-group contract.
	///
	/// Adding an absolute in-world table means adding it HERE as well: a
	/// swallowed entry send has no second chance until the next world-entry
	/// edge, which is the gap the trap-layout snapshot sat in (the sync-coverage
	/// audit's W6 row).
	/// </summary>
	public void SendInSessionRepair(ulong steamId)
	{
		_log.LogInformation("Sending the in-session repair group to {Peer}.", steamId);
		_world.SendBlockStateSnapshot(steamId);
		_world.SendBlockDamageSnapshot(steamId);
		_world.SendTrapLayoutSnapshot(steamId);
		_kernelProtocol.SendCheckpoint(steamId);
		_world.SendRuntimeEntitySnapshot(steamId);
	}
}
