using System.Collections.Generic;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;

namespace CasualtiesUnknownOnline.Runtime.Session;

/// <summary>
/// The entity-sync surface packet handlers operate on — implemented by
/// EntitySyncService. Handlers depend on this narrow interface instead of the
/// concrete service, which keeps the constructor graph acyclic (the entity
/// domain depends on SessionState/MemberPresenceTable, not on SessionService —
/// abstract extraction, user rule).
/// </summary>
public interface IEntitySyncControl
{
	PlayerEntity LocalPlayer { get; }

	uint LastStateSeq { get; set; }

	IEnumerable<EntitySyncService.SyncedEntity> Members { get; }

	bool TryGetSynced(ulong steamId, out EntitySyncService.SyncedEntity member);

	void ApplyEntityState(EntityStateMsg msg, PlayerEntity target);

	void FireStateReceived(PlayerEntity entity);

	void ProcessPlayerJoin(PlayerJoinMsg msg);

	void MaybeStartEntitySync();

	void EndMemberSync(ulong steamId);

	void EndEntitySync();
}
