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
/// </summary>
public sealed class WorldEntryFanout(
	IWorldControl world,
	IItemControl items,
	IEnemySyncControl enemies,
	ILogger<WorldEntryFanout> log)
{
	private readonly IWorldControl _world = world;
	private readonly IItemControl _items = items;
	private readonly IEnemySyncControl _enemies = enemies;
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
		_world.SendTrapStateSnapshot(steamId);
		_world.SendOpenedEntitiesSnapshot(steamId);
		_world.SendBuildingEntityHealthSnapshot(steamId);
		_world.SendTrapLayoutSnapshot(steamId);
		_world.SendRadiationLineState(steamId);
		_items.SendItemSnapshot(steamId);
		_enemies.SendEnemySnapshot(steamId);
		_world.SendWorldSnapshotComplete(steamId);
	}
}
