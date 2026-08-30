using CasualtiesUnknownOnline.Protocol.Wire;
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

	/// <summary>Guest side: apply an explicit enemy aggregate removal (the reliable lifecycle counterpart of the state stream).</summary>
	void ApplyEnemyRemoved(EnemyRemovedMsg msg);

	/// <summary>Guest side: apply the full enemy snapshot (world entry / late joiner — clears + repopulates).</summary>
	void ApplyEnemySnapshot(EnemySnapshotMsg msg);

	/// <summary>Host side: send the full enemy snapshot to one member (world entry / reconnect).</summary>
	void SendEnemySnapshot(ulong steamId);

	/// <summary>Host side: order one member to apply an enemy attack locally (the host's collision callbacks cannot reach a collider-less remote clone).</summary>
	void SendEnemyAttack(EnemyAttackMsg msg);

	/// <summary>A host-ordered enemy attack arrived at the victim — surface it for the Game Adapter to apply locally.</summary>
	void FireEnemyAttackReceived(EnemyAttackMsg msg);
}
