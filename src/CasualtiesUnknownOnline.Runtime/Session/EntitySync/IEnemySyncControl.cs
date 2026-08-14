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

	/// <summary>Guest side: apply a 20 Hz enemy-state batch (overwrite per id).</summary>
	void ApplyEnemyState(EnemyStateBatchMsg msg);

	/// <summary>Guest side: apply the full enemy snapshot (world entry / late joiner — clears + repopulates).</summary>
	void ApplyEnemySnapshot(EnemySnapshotMsg msg);

	/// <summary>Host side: send the full enemy snapshot to one member (world entry / reconnect).</summary>
	void SendEnemySnapshot(ulong steamId);

	/// <summary>Raise the enemy-state-received notification (the Game Adapter re-applies on this).</summary>
	void FireEnemyStateReceived();
}
