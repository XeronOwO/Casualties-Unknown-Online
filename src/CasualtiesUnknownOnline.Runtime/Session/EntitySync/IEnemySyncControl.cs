using CasualtiesUnknownOnline.Protocol.Wire;
using CasualtiesUnknownOnline.Runtime.Protocol;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;

namespace CasualtiesUnknownOnline.Runtime.Session.EntitySync;

/// <summary>
/// The enemy-sync surface packet handlers operate on — implemented by
/// <see cref="EnemySyncService"/>. Handlers depend on this narrow interface
/// instead of the concrete service (same abstract-extraction pattern as
/// <see cref="IEntitySyncControl"/>).
/// </summary>
public interface IEnemySyncControl
{
	/// <summary>Guest side: last applied enemy-state seq (the unreliable-stream gate).</summary>
	uint LastEnemyStateSeq { get; set; }

	/// <summary>Guest side: apply an update-only 20 Hz enemy-state stream (never removes an id absent from the stream).</summary>
	void ApplyEnemyStream(WireStateStream stream);

	/// <summary>Guest side: apply the full enemy snapshot (world entry / late joiner / the 60 s in-session repair — clears + repopulates; the generated copies pair on each entry's spawn anchor, so a late apply is idempotent).</summary>
	void ApplyEnemySnapshot(EnemySnapshotMsg msg);

	/// <summary>Host side: send the full enemy snapshot to one member (world entry / reconnect / the 60 s in-session repair group, so a swallowed entry send heals for a member that never leaves the world).</summary>
	void SendEnemySnapshot(ulong steamId);

	/// <summary>Host side: announce an enemy attack to every in-world guest — the host owns the enemy's action, while whether it connected is judged by each guest on its own view (the host's collision callbacks cannot reach a collider-less remote clone). The host stamps the per-enemy attack identity.</summary>
	void SendEnemyAttack(NetworkEntityId enemyId, EnemyAttackKind kind);

	/// <summary>An announced enemy attack arrived on this guest — surface it for the Game Adapter to judge against its own body and apply locally.</summary>
	void FireEnemyAttackReceived(EnemyAttackMsg msg);
}
