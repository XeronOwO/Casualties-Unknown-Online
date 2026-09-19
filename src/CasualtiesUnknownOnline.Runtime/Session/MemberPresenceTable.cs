using System;
using System.Collections.Generic;
using CasualtiesUnknownOnline.Runtime.Protocol;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using CasualtiesUnknownOnline.Runtime.Session.EntitySync;
using CasualtiesUnknownOnline.Runtime.Session.World;

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

		/// <summary>
		/// Host only: the handshake ack this host sent the member (null before the first
		/// ack). The warm-up pump re-sends it — with the scene field refreshed, the only
		/// field that moves — while the member stays unconfirmed, because the ack's other
		/// admission facts (the world-params presence, the assigned peer id, the host's
		/// own identity at ack time) are known only where the ack was built and nothing
		/// else re-drives the third handshake leg.
		/// </summary>
		public HandshakeAckMsg? SentHandshakeAck;

		/// <summary>Custom display name for IP-direct sessions (empty = fall back to Steam persona/ID).</summary>
		public string DisplayName = "";

		/// <summary>The member's selected marker color (null = automatic palette).</summary>
		public NetColorRgba? SelectedColor;

		/// <summary>
		/// Host only: the world-entry repair cadence of this member's CURRENT entry. Armed on
		/// the InWorld edge and consulted when a repeat scene report arrives while the member's
		/// readiness window is still open (<see cref="EntryRepairSchedule"/>); it is what keeps
		/// a still-open window answered in bounded steps instead of on every 5 s repeat.
		/// </summary>
		public readonly EntryRepairSchedule EntryRepair = new();
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
	/// Raised when a member's handshake completes (Handshaken flips true — every
	/// time, reconnects included: the presence table is stable across reconnects,
	/// so a rejoin re-fires it). The item domain subscribes to grant the id
	/// watermark on the host.
	/// </summary>
	public event Action<ulong>? MemberAdded;

	/// <summary>
	/// Raised when a member enters or leaves the world (inWorld=false pauses /
	/// destroys the render clone; a member leaving the session reuses
	/// inWorld=false so the clone teardown path is shared). The SteamId routes
	/// the event to the right clone.
	/// </summary>
	public event Action<ulong, bool>? RemoteSceneChanged;

	public void FireMemberRemoved(ulong steamId) => MemberRemoved?.Invoke(steamId);

	public void FireMemberAdded(ulong steamId) => MemberAdded?.Invoke(steamId);

	public void FireRemoteSceneChanged(ulong steamId, bool inWorld) => RemoteSceneChanged?.Invoke(steamId, inWorld);

	/// <summary>
	/// Raised on the host when a member that is ALREADY in the world re-asserted its scene
	/// report while its entry window was still open: the entry state it may have missed is
	/// re-sent on this signal. The runtime-owned absolute tables are re-sent by
	/// <see cref="WorldEntryFanout"/> at the same decision point; this event
	/// carries the half the Game Adapter owns (the keypad codes and the geyser liquid types,
	/// the same tables it re-fans-out on <see cref="RemoteSceneChanged"/>). A repeat exists only
	/// while the member's readiness window is open, so a window closed by BOTH control facts
	/// never fires it — a window a still-armed start gate holds open does, and
	/// <see cref="MemberPresence.EntryRepair"/> bounds that.
	/// </summary>
	public event Action<ulong>? EntryRepairRequested;

	public void FireEntryRepairRequested(ulong steamId) => EntryRepairRequested?.Invoke(steamId);
}
