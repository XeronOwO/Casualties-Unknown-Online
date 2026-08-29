using System.Collections.Generic;
using System.Linq;
using CasualtiesUnknownOnline.GameState.Domains.Entities;
using CasualtiesUnknownOnline.Runtime.Session.Items;

namespace CasualtiesUnknownOnline.Runtime.Session.EntitySync;

/// <summary>
/// Projects the host's enemy presentation/health facts into the kernel entity
/// table. The projection is change-gated so the per-frame publish call does not
/// spam kernel commits.
/// </summary>
public sealed class EnemyKernelProjection(
	ItemKernelAuthority kernelAuthority,
	ISessionControl session)
{
	private readonly ItemKernelAuthority _kernelAuthority = kernelAuthority;
	private readonly ISessionControl _session = session;

	public void Sync(IEnumerable<EnemyEntity> enemies)
	{
		var desired = enemies.Select(ToKernelState).ToList();
		var table = _kernelAuthority.QueryEnemies();

		foreach (var state in desired)
		{
			var current = table?.Enemies.FirstOrDefault(e => e.EntityId == state.EntityId);
			if (current is not null
				&& current.PrefabId == state.PrefabId
				&& current.Health == state.Health
				&& current.RuntimeSpawned == state.RuntimeSpawned
				&& current.Stunned == state.Stunned)
			{
				continue;
			}

			_kernelAuthority.TryUpsertEnemy(_session.LocalSteamId, state, out _, out _);
		}

		if (table is null)
		{
			return;
		}

		var desiredIds = desired.Select(e => e.EntityId).ToHashSet();
		foreach (var stale in table.Enemies.Where(e => !desiredIds.Contains(e.EntityId)))
		{
			_kernelAuthority.TryRemoveEnemy(_session.LocalSteamId, stale.EntityId, out _, out _);
		}
	}

	private static EnemyState ToKernelState(EnemyEntity entity) =>
		new(
			new EntityId(entity.EntityId.Epoch, entity.EntityId.Counter, entity.EntityId.Generation),
			entity.PrefabId,
			entity.Health,
			entity.RuntimeSpawned,
			entity.Stunned);
}
