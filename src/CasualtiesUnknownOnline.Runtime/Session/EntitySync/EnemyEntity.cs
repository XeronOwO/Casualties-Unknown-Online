using CasualtiesUnknownOnline.Runtime.Protocol;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;

namespace CasualtiesUnknownOnline.Runtime.Session.EntitySync;

/// <summary>
/// Session-side buffer of one enemy's authoritative presentation state: the host
/// simulates the enemy (AI + physics) and writes this buffer; the guest reads it
/// to drive its frozen render copy. Only the presentation subset is held —
/// position / velocity / rotation / health + a few animation flags — NOT the AI
/// internal state (target / stun timers). No Unity references; the Game Adapter
/// converts at the boundary.
/// </summary>
public sealed class EnemyEntity(NetworkEntityId entityId)
{
	public NetworkEntityId EntityId { get; set; } = entityId;

	public NetVector2 Position { get; set; }

	public NetVector2 Velocity { get; set; }

	/// <summary>Facing (z euler angle, degrees — the Rigidbody2D rotation).</summary>
	public float Rotation { get; set; }

	public float Health { get; set; }

	/// <summary>True while the enemy presents a stunned/stuck pose (SpiderHandler.stunTime &gt; 0, CrystalEnemy.stuck).</summary>
	public bool Stunned { get; set; }

	/// <summary>The prefab id (BuildingEntity.id) the receiving side must instantiate when it has no local copy of a runtime-created enemy.</summary>
	public string PrefabId { get; set; } = string.Empty;

	/// <summary>True when this enemy was created at RUNTIME (outside generation) — the late-joiner snapshot carries it as an EnemySpawnEntryMsg so a fresh member can materialize the copy.</summary>
	public bool RuntimeSpawned { get; set; }

	/// <summary>Domain → wire; the reverse applies via <see cref="EnemyStateMsg.ApplyTo"/>.</summary>
	public EnemyStateMsg ToEnemyStateMsg() => new()
	{
		Id = EntityId.ToNetworkEntityIdMsg(),
		Position = Position.ToNetVector2Msg(),
		Velocity = Velocity.ToNetVector2Msg(),
		Rotation = Rotation,
		Health = Health,
		PresentationFlags = Stunned ? EnemyStateMsg.FlagStunned : 0u,
	};

	/// <summary>The runtime-spawn backfill entry (only meaningful when <see cref="RuntimeSpawned"/> is true).</summary>
	public EnemySpawnEntryMsg ToEnemySpawnEntryMsg() => new()
	{
		Id = EntityId.ToNetworkEntityIdMsg(),
		PrefabId = PrefabId,
		Position = Position.ToNetVector2Msg(),
		Rotation = Rotation,
	};
}
