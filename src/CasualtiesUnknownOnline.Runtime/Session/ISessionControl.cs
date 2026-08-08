using System.Collections.Generic;
using CasualtiesUnknownOnline.Runtime.Protocol;

namespace CasualtiesUnknownOnline.Runtime.Session;

/// <summary>
/// The session control surface packet handlers operate on — implemented by
/// SessionService. Handlers depend on this narrow interface instead of the
/// concrete service, which keeps the constructor graph acyclic (the session's
/// own constructor depends on the packet gateway; the gateway's router builds
/// handlers — abstract extraction, user rule).
/// </summary>
public interface ISessionControl
{
	SessionRole Role { get; }

	bool SessionActive { get; set; }

	ulong HostSteamId { get; }

	ulong LocalSteamId { get; }

	bool LocalInWorld { get; }

	SceneStateType LocalSceneState { get; }

	WorldStartParams? WorldParams { get; set; }

	float LastRttMs { get; }

	IEnumerable<MemberPresenceTable.MemberPresence> Members { get; }

	bool TryGetMember(ulong steamId, out MemberPresenceTable.MemberPresence member);

	MemberPresenceTable.MemberPresence GetOrCreateMember(ulong steamId);

	bool IsLobbyMember(ulong steamId);

	void Broadcast(NetMsg msg, object payload);

	void BroadcastExcept(ulong excludeSteamId, NetMsg msg, object payload);

	void RemoveGuestMember(ulong steamId);

	void EndSession();

	void RecordPong(ulong sender, long ticks);

	void FireSessionActivated();

	void FireRemoteSceneChanged(ulong steamId, bool inWorld);

	void FireBlockDamagedReceived(NetVector2 pos, float damage);
}
