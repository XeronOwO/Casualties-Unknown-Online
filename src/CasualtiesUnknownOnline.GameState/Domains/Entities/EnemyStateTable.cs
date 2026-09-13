using System.Collections.Generic;
using System.Linq;

namespace CasualtiesUnknownOnline.GameState.Domains.Entities;

/// <summary>
/// Immutable enemy/entity fact table. Reducers produce new snapshots so the
/// kernel can swap atomically. <c>Removed</c> holds terminal tombstones: an
/// enemy id once removed cannot be resurrected until the table is reset — and
/// "until the table is reset" means a SESSION reset, not a layer boundary: a
/// layer boundary drops the live rows (<see cref="WithoutLiveEnemies"/>) and keeps
/// the tombstones, because "this enemy was killed" is a terminal fact the player
/// earned, not a fact about one layer's layout.
/// </summary>
public sealed record EnemyStateTable(
	IReadOnlyList<EnemyState> Enemies,
	IReadOnlyList<EntityId> Removed)
{
	public static readonly EnemyStateTable Empty = new([], []);

	public bool IsRemoved(EntityId entityId) => Removed.Contains(entityId);

	/// <summary>
	/// The LAYER-BOUNDARY shape of the table: the live rows belong to the layer being
	/// left (they name its layout, and the host's per-session id counter keeps
	/// allocating), while the tombstones are terminal facts that stay — a killed id
	/// must not be resurrected by a stale live row of the layer that killed it.
	/// </summary>
	public EnemyStateTable WithoutLiveEnemies() => this with { Enemies = [] };

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
