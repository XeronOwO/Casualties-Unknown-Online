using System.Collections.Generic;
using System.Linq;

namespace CasualtiesUnknownOnline.GameState.Domains.Entities;

/// <summary>
/// Immutable enemy/entity fact table. Reducers produce new snapshots so the
/// kernel can swap atomically.
/// </summary>
public sealed record EnemyStateTable(IReadOnlyList<EnemyState> Enemies)
{
	public static readonly EnemyStateTable Empty = new([]);

	public EnemyStateTable Upsert(EnemyState state) =>
		this with
		{
			Enemies = [.. Enemies.Where(e => e.EntityId != state.EntityId), state],
		};

	public EnemyStateTable Remove(EntityId entityId) =>
		this with
		{
			Enemies = [.. Enemies.Where(e => e.EntityId != entityId)],
		};
}
