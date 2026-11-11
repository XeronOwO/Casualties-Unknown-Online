using System;
using System.Collections.Generic;
using CasualtiesUnknownOnline.Abstractions;
using CasualtiesUnknownOnline.Runtime.Localization;
using CasualtiesUnknownOnline.Runtime.Networking;
using Microsoft.Extensions.Logging;

namespace CasualtiesUnknownOnline.Runtime.Session;

/// <summary>
/// Session side of the transport's link-loss edge: when the active connection to a peer ends —
/// its socket closed, its read loop finished, the address went away — the session must stop
/// counting that peer. On the host the member is removed, so the start gate stops waiting for a
/// peer that will never finish loading and the member's reconnected identity is admitted by the
/// normal handshake. On the guest a lost host link ends the session, because nothing it reports
/// can reach the host any more.
///
/// <para>
/// The transport reports the fact; what it means for the session is decided here, exactly as the
/// send-stall policy does for a queue that refuses everything.
/// </para>
/// </summary>
internal sealed class PeerDisconnectWatchdog : ICuoService, ISessionReset
{
	/// <summary>The reason a member whose connection ended is removed with (log + Kicked message).</summary>
	internal const string LinkLostReason = "the transport lost this peer's connection";

	/// <summary>
	/// How long a lost host link may stay down before the member is dropped. The IP transport raises
	/// the edge when an OLD connection closes (a retry, a scene change) and opens a new one right
	/// after; acting on the edge alone kicked a member that had just finished its handshake
	/// (measured 2026-11-12: `Dropping <peer> (Guest) … the transport lost this peer's connection`
	/// three lines after `Handshake confirmed end-to-end`). The edge is a fact; the member is only
	/// removed when the link is still down after the grace.
	/// </summary>
	internal static readonly TimeSpan DropGrace = TimeSpan.FromSeconds(10);

	private readonly IPeerDisconnectSource _router;
	private readonly ISessionControl _session;
	private readonly ISessionNotices _notices;
	private readonly ILocalizationService _localization;
	private readonly ILogger<PeerDisconnectWatchdog> _log;
	private readonly Dictionary<ulong, long> _pendingDrops = [];
	private long _nowMs;

	public PeerDisconnectWatchdog(
		IPeerDisconnectSource router,
		ISessionControl session,
		ISessionNotices notices,
		ILocalizationService localization,
		ILogger<PeerDisconnectWatchdog> log)
	{
		_router = router;
		_session = session;
		_notices = notices;
		_localization = localization;
		_log = log;
		_router.PeerDisconnected += OnPeerDisconnected;
		_session.SessionEnded += ResetSessionState;
	}

	void ICuoService.Initialize()
	{
	}

	void ICuoService.Start()
	{
	}

	/// <summary>The grace: the edge is acted on only when the link is still down after it.</summary>
	void ICuoService.Update()
	{
		_nowMs = NowMs();
		if (_pendingDrops.Count == 0)
		{
			return;
		}

		var due = new List<ulong>();
		foreach (var entry in _pendingDrops)
		{
			if (_nowMs >= entry.Value)
			{
				due.Add(entry.Key);
			}
		}

		ulong? drop = null;
		foreach (var peer in due)
		{
			if (_router.IsConnected(peer))
			{
				// The link came back inside the grace (a retry, a reconnect): the member stays.
				_pendingDrops.Remove(peer);
				_log.LogInformation("The link to {Peer} came back inside the drop grace; the member stays.", peer);
				continue;
			}

			drop ??= peer;
		}

		if (drop is { } victim)
		{
			_pendingDrops.Remove(victim);
			DecideAndAct(victim);
		}
	}

	void ICuoService.Stop()
	{
	}

	public void Dispose()
	{
		_router.PeerDisconnected -= OnPeerDisconnected;
		_session.SessionEnded -= ResetSessionState;
	}

	/// <summary>A finished session carries no link facts: the edge answers only the session it arrived in.</summary>
	public void ResetSessionState()
	{
	}

	/// <summary>What a lost link means for the session, decided without touching it.</summary>
	internal static PeerLossAction Decide(SessionRole role, ulong localPeer, ulong hostPeer, ulong peer, bool knownMember)
	{
		if (role == SessionRole.Host)
		{
			// Only a member the host still knows: an edge that arrives after the member already
			// left (or for the host itself) has nothing to remove.
			return peer != localPeer && knownMember ? PeerLossAction.DropMember : PeerLossAction.None;
		}

		return role == SessionRole.Guest && peer == hostPeer ? PeerLossAction.EndSession : PeerLossAction.None;
	}

	private void OnPeerDisconnected(ulong peer)
	{
		var known = _session.TryGetMember(peer, out _);
		if (Decide(_session.Role, _session.LocalSteamId, _session.HostSteamId, peer, known) == PeerLossAction.None)
		{
			return;
		}

		// The edge says the connection ended; whether the member is gone is decided after the grace,
		// because a retry opens a new connection to the same peer within it.
		_pendingDrops[peer] = NowMs() + (long)DropGrace.TotalMilliseconds;
	}

	private static long NowMs() => DateTime.UtcNow.Ticks / TimeSpan.TicksPerMillisecond;

	private void DecideAndAct(ulong peer)
	{
		var known = _session.TryGetMember(peer, out var member);
		switch (Decide(_session.Role, _session.LocalSteamId, _session.HostSteamId, peer, known))
		{
			case PeerLossAction.DropMember:
				DropLostMember(peer, member!.DisplayName);
				break;

			case PeerLossAction.EndSession:
				_log.LogWarning("The host {Host} link ended — ending the session locally.", peer);
				_notices.Publish(_localization.Format("hud.peer_link_lost", peer.ToString()));
				_session.EndSession();
				break;
		}
	}

	private void DropLostMember(ulong peer, string displayName)
	{
		var name = displayName.Length > 0 ? displayName : peer.ToString();
		_log.LogWarning("Dropping {Peer} ({Name}) from the session: {Reason}.", peer, name, LinkLostReason);
		if (_session.KickMember(peer, LinkLostReason))
		{
			_notices.Publish(_localization.Format("hud.peer_link_lost", name));
		}
	}
}
