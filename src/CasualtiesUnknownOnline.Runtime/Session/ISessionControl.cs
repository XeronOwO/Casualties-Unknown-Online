using System;
using System.Collections.Generic;
using CasualtiesUnknownOnline.Runtime.Protocol;

namespace CasualtiesUnknownOnline.Runtime.Session;

/// <summary>
/// The session control surface packet handlers, the entity/data domains and
/// the receiver operate on — implemented by SessionService. Depends on this
/// narrow interface instead of the concrete service: the interface is resolved
/// lazily by the container after the session itself is built, which keeps the
/// constructor graph acyclic (abstract extraction, user rule).
/// </summary>
public interface ISessionControl
{
	SessionRole Role { get; }

	bool SessionActive { get; set; }

	ulong HostSteamId { get; }

	ulong LocalSteamId { get; }

	bool LocalInWorld { get; }

	SceneStateType LocalSceneState { get; }

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

	/// <summary>Raised when a member is removed from the presence table (the entity domain cleans up on this).</summary>
	event Action<ulong>? MemberRemoved;

	/// <summary>Raised when the session ends (the entity domain tears down on this).</summary>
	event Action? SessionEnded;
}
