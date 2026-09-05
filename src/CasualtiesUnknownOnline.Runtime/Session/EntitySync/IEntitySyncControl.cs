using System;
using System.Collections.Generic;
using CasualtiesUnknownOnline.Protocol.Wire;
using CasualtiesUnknownOnline.Runtime.Protocol;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;

namespace CasualtiesUnknownOnline.Runtime.Session.EntitySync;

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

	/// <summary>All synced remote entities (host: one per guest; guest: host + roster guests).</summary>
	IReadOnlyList<PlayerEntity> RemotePlayers { get; }

	/// <summary>Synced remote entity by SteamId, or null.</summary>
	PlayerEntity? GetRemotePlayer(ulong steamId);

	/// <summary>Host side: current authoritative state of the local body (the stream's source surface).</summary>
	void PublishLocalState(NetVector2 position, NetVector2 lookPos, NetVector2 velocity,
		bool isRight, bool standing, bool alive, bool conscious, bool crouching,
		NetVector2? lookOverridePos = null, float lookOverrideTime = 0f, float eyeScareTime = 0f,
		float eyePanicTime = 0f, float eyeCloseTime = 0f,
		bool sitting = false, bool sleeping = false, bool climbing = false,
		byte workoutType = 0, byte napVariant = 0, float dogShakeIntensity = 0f,
		bool slidingLeft = false, bool slidingRight = false,
		List<PlayerLimbPose>? limbPoses = null);

	/// <summary>The local player swung — mark the swing so peers replay the animation via the snapshot flag + sequence.</summary>
	void MarkLocalAttackSwing();

	/// <summary>Raised when a member's entity sync starts (host: that guest; guest: host or a roster member).</summary>
	event Action<PlayerEntity>? RemoteJoined;

	uint LastStateSeq { get; set; }

	IEnumerable<EntitySyncService.SyncedEntity> Members { get; }

	bool TryGetSynced(ulong steamId, out EntitySyncService.SyncedEntity member);

	void ApplyPlayerState(WirePlayerStreamState msg, PlayerEntity target);

	void FireStateReceived(PlayerEntity entity);

	void ProcessPlayerJoin(PlayerJoinMsg msg);

	void MaybeStartEntitySync();

	void EndMemberSync(ulong steamId);

	void EndEntitySync();
}
