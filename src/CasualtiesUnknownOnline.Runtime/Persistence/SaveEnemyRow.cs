using CasualtiesUnknownOnline.GameState.Domains.Entities;
using CasualtiesUnknownOnline.Protocol.Wire;
using CasualtiesUnknownOnline.Runtime.Session.Items;

namespace CasualtiesUnknownOnline.Runtime.Persistence;

/// <summary>
/// One row of <c>enemies.json</c> (§3.4). The kernel's enemy table holds two
/// shapes of fact — a live enemy state, and the terminal tombstone that stops a
/// removed enemy from ever being resurrected — so the file is a typed row list
/// and each row is salvaged on its own. A tombstone is not decoration: losing
/// one would let a killed enemy come back on restore (acceptance: terminal
/// facts stay terminal).
/// </summary>
public sealed class SaveEnemyRow
{
	/// <summary>The row kind: <c>enemy</c> or <c>removed</c>.</summary>
	public string Kind { get; init; } = string.Empty;

	public WireEnemyState? Enemy { get; init; }

	public WireEntityId? Removed { get; init; }

	public static SaveEnemyRow OfEnemy(EnemyState state) =>
		new() { Kind = EnemyKind, Enemy = KernelDomainWireMapper.ToWireEnemyState(state) };

	public static SaveEnemyRow OfRemoved(EntityId entityId) =>
		new() { Kind = RemovedKind, Removed = KernelDomainWireMapper.ToWireEntityId(entityId) };

	/// <summary>The row's identity for the damage report: the entity id, plus the prefab a live enemy names.</summary>
	public string Describe() => Kind switch
	{
		RemovedKind => $"removed enemy {EntityId(Removed)}",
		_ => string.IsNullOrEmpty(Enemy?.PrefabId) ? $"enemy {EntityId(Enemy?.EntityId)}" : $"enemy {Enemy!.PrefabId} {EntityId(Enemy.EntityId)}",
	};

	private static string EntityId(WireEntityId? id) =>
		id is null ? "<no id>" : $"(epoch {id.Epoch}, counter {id.Counter}, generation {id.Generation})";

	public const string EnemyKind = "enemy";
	public const string RemovedKind = "removed";
}
