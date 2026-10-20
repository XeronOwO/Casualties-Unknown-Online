using System;
using System.Collections.Generic;
using CasualtiesUnknownOnline.Runtime.Protocol;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using CasualtiesUnknownOnline.Runtime.Time;
using Microsoft.Extensions.Logging;

namespace CasualtiesUnknownOnline.Runtime.Session.World;

/// <summary>
/// The world re-entry request's message flow. A member's entry into a world is driven by the
/// host's <c>WorldJoin</c>, which is an EDGE: the host sends it at its click and at its own
/// world-entry transition, and a member can miss it (the lazy-P2P swallow) or discard it against
/// its own state — an invite that arrives while the member is still inside its old world waits at
/// the entry gate and is dropped when that world unloads. A layer change is the production case:
/// the host's regeneration pulls every member to the menu and re-invites them on its way back, and
/// a member whose invite was lost stays in the menu with no body (batch `20261007-a`'s
/// unprotected member was still out at +60 s).
/// <para>
/// The member reports the fact the host cannot see — it is out and its exit was the host's own
/// transition, not its own choice — and the host answers with the SAME instruction every other
/// entry uses. The admission is bounded by what this host can establish itself: a handshaken
/// member, held OUT of the world by this host's own presence table (a member it still holds IN
/// world is settled by the bounded scene re-report, not by an invitation it would ignore), and a
/// per-sender cooldown, because the answer is a world-entry instruction plus a checkpoint set and
/// a member's client must not be able to make the host ship one per frame. Whether this host has
/// a live world and a published run baseline is not a session fact — that half is the world
/// domain's (<see cref="IReentryControl.ReentryRequested"/>).
/// </para>
/// </summary>
public sealed class ReentryRequestChannel : IReentryControl, ISessionReset, IDisposable
{
	/// <summary>The per-sender cadence: a member's own window asks every few seconds, so a repeated ask inside this bound is one client talking to itself — and the answer costs a checkpoint set, which is what the bound protects.</summary>
	internal const long MinIntervalMs = 2_000;

	private readonly ISessionControl _session;
	private readonly PacketSender _sender;
	private readonly ITimeSource _time;
	private readonly ILogger<ReentryRequestChannel> _log;

	/// <summary>When each sender was last heard — every processed ask spends the cadence, the refused ones included, so a client asking every frame costs one line and no work per window, not per frame.</summary>
	private readonly Dictionary<ulong, long> _lastSpokenMs = [];

	public ReentryRequestChannel(ISessionControl session, PacketSender sender, ITimeSource time, ILogger<ReentryRequestChannel> log)
	{
		_session = session;
		_sender = sender;
		_time = time;
		_log = log;
		_session.SessionEnded += ResetSessionState;
	}

	/// <summary>Host only: a member's re-entry ask was admitted (handshaken, held out of the world, outside the cooldown) — the world domain answers it with this host's own facts.</summary>
	public event Action<ulong>? ReentryRequested;

	/// <summary>Any role: ask the host to re-issue this member's world-entry instruction. Guest in a live session only; the caller keeps its bounded window and this reports whether anything left the client (a refused link must not spend the window).</summary>
	public bool TrySendReentryRequest()
	{
		if (_session.Role != SessionRole.Guest || !_session.SessionActive)
		{
			return false;
		}

		if (!_sender.TrySend(_session.HostSteamId, NetMsg.ReentryRequest, new ReentryRequestMsg()))
		{
			_log.LogWarning("[Reentry] the ask did not leave this client (the host link refused the frame) — the window will try again.");
			return false;
		}

		_log.LogInformation("[Reentry] asked the host to re-issue this member's world entry — this member is out of the world after the host's own exit.");
		return true;
	}

	/// <summary>Host only: a member says it is out of the world and asks to be taken back in — admit it only against this host's own membership table, and only outside the per-sender cadence.</summary>
	public void HandleReentryRequest(ulong senderId)
	{
		if (_session.Role != SessionRole.Host)
		{
			return;
		}

		var nowMs = _time.NowMs;
		if (_lastSpokenMs.TryGetValue(senderId, out var lastMs) && lastMs <= nowMs && nowMs - lastMs < MinIntervalMs)
		{
			// Debug, not Information, and BEFORE every refusal branch: an honest member's window asks
			// every few seconds and one ask per burst is answered; a client that asks every frame —
			// admitted or refused — costs one line and no work per window rather than per frame.
			// `lastMs <= nowMs` is the TickCount wrap (~24.9 days): a reading that went backwards is
			// not a cooldown.
			_log.LogDebug("[Reentry] {Sender}'s re-entry request is inside the {Interval} ms cadence — dropped; the answer to the earlier ask is the answer to this one.", senderId, MinIntervalMs);
			return;
		}

		_lastSpokenMs[senderId] = nowMs;
		if (!_session.TryGetMember(senderId, out var member) || !member.Handshaken)
		{
			_log.LogWarning("[Reentry] {Sender}'s re-entry request was refused: not a handshaken member of this session.", senderId);
			return;
		}

		if (member.InWorld)
		{
			// The host's own presence table is the bound: a member it holds IN the world asking to be
			// let in is a member whose exit report has not arrived (or whose view is stale). The
			// bounded scene re-report is what settles that, and an invitation sent now would be
			// discarded by the member's own entry gate while it is still in its world.
			_log.LogInformation("[Reentry] {Sender}'s re-entry request was refused: this host holds the member IN the world; its scene re-report settles the two sides.", senderId);
			return;
		}

		_log.LogInformation("[Reentry] {Sender} is out of the world and asks to be taken back in — the world half is this host's to answer.", senderId);
		ReentryRequested?.Invoke(senderId);
	}

	/// <summary>The session is gone — the cadence table belongs to it.</summary>
	public void ResetSessionState() => _lastSpokenMs.Clear();

	public void Dispose() => _session.SessionEnded -= ResetSessionState;
}
