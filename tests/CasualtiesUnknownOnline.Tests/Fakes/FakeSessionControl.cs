using System;
using System.Collections.Generic;
using System.Linq;
using CasualtiesUnknownOnline.Runtime.Protocol;
using CasualtiesUnknownOnline.Runtime.Session;

namespace CasualtiesUnknownOnline.Tests.Fakes;

/// <summary>
/// A minimal <see cref="ISessionControl"/> for tests that need the session's
/// identity/roster shape without a network: the transport-scoped save tests and
/// the continue tests read <see cref="Role"/>, <see cref="LocalSteamId"/> and
/// <see cref="Members"/>. Everything a real session would do (broadcasts,
/// lobby queries, kicks) is unreachable from those tests and throws, so a test
/// that drifts into a real session path fails loudly instead of silently
/// passing on a stub.
/// </summary>
internal sealed class FakeSessionControl : ISessionControl
{
	private readonly List<MemberPresenceTable.MemberPresence> _members = [];

	public SessionRole Role { get; set; } = SessionRole.Host;

	public bool SessionActive { get; set; } = true;

	public ulong HostSteamId { get; set; } = 1UL;

	public ulong LocalSteamId { get; set; } = 1UL;

	public bool LocalInWorld { get; set; } = true;

	public SceneStateType LocalSceneState { get; set; } = SceneStateType.InWorld;

	public float LastRttMs => 0f;

	public IEnumerable<MemberPresenceTable.MemberPresence> Members => _members;

	/// <summary>Adds a handshaken member with the given display name (the key the IP-direct mode claims by).</summary>
	public MemberPresenceTable.MemberPresence AddMember(ulong steamId, string displayName, bool handshaken = true, bool inWorld = true)
	{
		var member = new MemberPresenceTable.MemberPresence
		{
			SteamId = steamId,
			DisplayName = displayName,
			Handshaken = handshaken,
			InWorld = inWorld,
		};
		_members.Add(member);
		return member;
	}

	public bool IsRemoteInWorld(ulong steamId) => TryGetMember(steamId, out var member) && member.InWorld;

	public NetVector2 GetRemoteSpawnPos(ulong steamId) => TryGetMember(steamId, out var member) ? member.ReportedSpawnPos : default;

	public void ReportSceneState(SceneStateType state, string sceneName, NetVector2? localPosition = null)
	{
		LocalSceneState = state;
		LocalSceneReported?.Invoke(state); // the real service announces the report; the fake mirrors it
	}

	public event Action<SceneStateType>? LocalSceneReported;

	public bool ResendSceneState() => throw new NotSupportedException("the save tests never re-report a scene state");

	public bool TryGetMember(ulong steamId, out MemberPresenceTable.MemberPresence member)
	{
		member = _members.FirstOrDefault(candidate => candidate.SteamId == steamId)!;
		return member is not null;
	}

	public MemberPresenceTable.MemberPresence GetOrCreateMember(ulong steamId) =>
		TryGetMember(steamId, out var member) ? member : AddMember(steamId, string.Empty);

	public bool IsLobbyMember(ulong steamId) => TryGetMember(steamId, out _);

	public void RemoveGuestMember(ulong steamId) => _members.RemoveAll(member => member.SteamId == steamId);

	public void FireSessionActivated() => SessionActivated?.Invoke();

	public void FireRemoteSceneChanged(ulong steamId, bool inWorld) => RemoteSceneChanged?.Invoke(steamId, inWorld);

	public void FireMemberAdded(ulong steamId) => MemberAdded?.Invoke(steamId);

	public event Action<ulong, bool>? RemoteSceneChanged;

	public void FireEntryRepairRequested(ulong steamId) => EntryRepairRequested?.Invoke(steamId);

	public event Action<ulong>? EntryRepairRequested;

	public void FireMemberRemoved(ulong steamId) => MemberRemoved?.Invoke(steamId);

	public event Action<ulong>? MemberRemoved;

	public event Action<ulong>? MemberAdded;

	public void FireSessionEnded() => SessionEnded?.Invoke();

	public event Action? SessionEnded;

	public event Action? SessionActivated;

	public void Broadcast(NetMsg msg, object payload) => throw new NotSupportedException("the save tests never broadcast");

	public void BroadcastExcept(ulong excludeSteamId, NetMsg msg, object payload) => throw new NotSupportedException("the save tests never broadcast");

	public bool KickMember(ulong steamId, string reason) => throw new NotSupportedException("the save tests never kick");

	public void EndSession() => throw new NotSupportedException("the save tests never end a session");

	public void RecordPong(ulong sender, long ticks) => throw new NotSupportedException("the save tests never record a pong");
}
