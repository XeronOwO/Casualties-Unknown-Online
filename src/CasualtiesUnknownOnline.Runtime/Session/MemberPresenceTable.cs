using System;
using System.Collections.Generic;
using CasualtiesUnknownOnline.Runtime.Protocol;

namespace CasualtiesUnknownOnline.Runtime.Session;

/// <summary>
/// The member presence table (key = SteamId, stable across reconnects) plus
/// the scene state of each member and the removal/scene events. Extracted from
/// SessionService so the entity/data domains read and mutate presence without
/// depending on SessionService itself (acyclic constructor graph, user rule:
/// abstract extraction, never AttachXxx wiring).
/// </summary>
public sealed class MemberPresenceTable
{
	/// <summary>
	/// One remote peer's session presence. Host: one entry per guest. Guest: one
	/// for the host plus roster entries for the other guests. Scene state
	/// (InWorld/ReportedSpawnPos) is session-scoped; the entity side (buffers,
	/// ids, sync state) is tracked by <see cref="EntitySyncService"/>.
	/// </summary>
	public sealed class MemberPresence
	{
		public ulong SteamId;
		public bool Handshaken; // protocol handshake completed
		public bool InWorld; // in the world (menu/loading = false)
		public NetVector2 ReportedSpawnPos; // position reported when entering the world — the clone anchor
		public float RttMs = -1f; // per-member ping diagnostics
	}

	private readonly Dictionary<ulong, MemberPresence> _members = [];

	public IEnumerable<MemberPresence> Members => _members.Values;

	public int Count => _members.Count;

	public bool TryGetMember(ulong steamId, out MemberPresence member) =>
		_members.TryGetValue(steamId, out member!);

	public MemberPresence GetOrCreateMember(ulong steamId)
	{
		if (!_members.TryGetValue(steamId, out var member))
		{
			member = new MemberPresence { SteamId = steamId };
			_members[steamId] = member;
		}

		return member;
	}

	public void Remove(ulong steamId) => _members.Remove(steamId);

	public void Clear() => _members.Clear();

	/// <summary>Raised when a member is removed from the presence table (left the
	/// lobby, PlayerLeave, …). The entity domain subscribes to drop the member's
	/// entity and announce the leave.</summary>
	public event Action<ulong>? MemberRemoved;

	/// <summary>
	/// Raised when a member enters or leaves the world (inWorld=false pauses /
	/// destroys the render clone; a member leaving the session reuses
	/// inWorld=false so the clone teardown path is shared). The SteamId routes
	/// the event to the right clone.
	/// </summary>
	public event Action<ulong, bool>? RemoteSceneChanged;

	public void FireMemberRemoved(ulong steamId) => MemberRemoved?.Invoke(steamId);

	public void FireRemoteSceneChanged(ulong steamId, bool inWorld) => RemoteSceneChanged?.Invoke(steamId, inWorld);
}
