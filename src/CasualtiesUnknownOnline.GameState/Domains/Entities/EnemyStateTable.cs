using System.Collections.Generic;
using System.Linq;

namespace CasualtiesUnknownOnline.GameState.Domains.Entities;

/// <summary>
/// Immutable enemy/entity fact table. Reducers produce new snapshots so the
/// kernel can swap atomically. <c>Removed</c> holds terminal tombstones: an
/// enemy id once removed cannot be resurrected until the table is reset.
/// </summary>
public sealed record EnemyStateTable(
	IReadOnlyList<EnemyState> Enemies,
	IReadOnlyList<EntityId> Removed)
{
	public static readonly EnemyStateTable Empty = new([], []);

	public bool IsRemoved(EntityId entityId) => Removed.Contains(entityId);

	public EnemyStateTable Upsert(EnemyState state) =>
		IsRemoved(state.EntityId)
			? this
			: this with
			{
				Enemies = [.. Enemies.Where(e => e.EntityId != state.EntityId), state],
			};

	public EnemyStateTable Remove(EntityId entityId) =>
		this with
		{
			Enemies = [.. Enemies.Where(e => e.EntityId != entityId)],
			Removed = Removed.Contains(entityId) ? Removed : [.. Removed, entityId],
		};
}
