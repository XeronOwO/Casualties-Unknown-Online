using System.Collections.Generic;
using System.Linq;
using CasualtiesUnknownOnline.GameState.Domains.Entities;
using CasualtiesUnknownOnline.Runtime.Session.Items;

namespace CasualtiesUnknownOnline.Runtime.Session.EntitySync;

/// <summary>
/// Projects authoritative kernel enemy facts into the runtime
/// <see cref="EnemyEntity"/> buffers used by restore/reconnect snapshot paths.
/// The kernel owns durable enemy identity/health/runtime-spawn/stunned facts;
/// the runtime buffer continues to own the continuous presentation fields
/// (position, velocity, rotation, spider-leg targets, telegraph state).
/// </summary>
public sealed class EnemyKernelRestoreProjection(
	ItemKernelAuthority kernelAuthority)
{
	private readonly ItemKernelAuthority _kernelAuthority = kernelAuthority;

	public void Apply(IEnumerable<EnemyEntity> enemies)
	{
		foreach (var enemy in enemies)
		{
			Apply(enemy);
		}
	}

	public void Apply(EnemyEntity enemy)
	{
		var current = _kernelAuthority.QueryEnemies()?.Enemies.FirstOrDefault(e => e.EntityId == ToEntityId(enemy.EntityId));
		if (current is null)
		{
			return;
		}

		enemy.Health = current.Health;
		enemy.Stunned = current.Stunned;
		enemy.PrefabId = current.PrefabId;
		enemy.RuntimeSpawned = current.RuntimeSpawned;
	}

	private static EntityId ToEntityId(NetworkEntityId id) =>
		new(id.Epoch, id.Counter, id.Generation);
}
