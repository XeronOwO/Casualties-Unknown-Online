using CasualtiesUnknownOnline.Abstractions;
using CasualtiesUnknownOnline.Runtime.Localization;
using CasualtiesUnknownOnline.Runtime.Networking;
using CasualtiesUnknownOnline.Runtime.Steam;
using Microsoft.Extensions.Logging;

namespace CasualtiesUnknownOnline.Runtime.Session;

/// <summary>
/// Session side of the send-stall policy: when the transport reports that a peer
/// has refused every send for the stall bound, the session degrades on the side
/// that owns the stall.
///
/// <para>
/// On the host, that member is removed with a named reason and the host's own
/// session continues — one guest whose link cannot drain must not take the host
/// down with it — and the status line is told who left and why. The kick is
/// best-effort by nature: the <c>Kicked</c> message rides the same link that has
/// been refusing everything, which is why the guest also carries its own
/// <see cref="GuestHostSilenceWatchdog"/>.
/// </para>
///
/// <para>
/// On the guest, a stall towards the host means nothing it reports can reach the
/// host any more: the guest ends its own session with a named reason instead of
/// playing on into a divergence only it can see.
/// </para>
/// </summary>
internal sealed class PeerSendStallWatchdog : ICuoService, ISessionReset
{
	/// <summary>The reason string a stalled member is removed with (log + Kicked message).</summary>
	internal const string StallReason = "the Steam send queue refused every message for the stall bound";

	private readonly ISendStallSource _stallSource;
	private readonly ISessionControl _session;
	private readonly ISteamService _steam;
	private readonly ISessionNotices _notices;
	private readonly ILocalizationService _localization;
	private readonly ILogger<PeerSendStallWatchdog> _log;

	public PeerSendStallWatchdog(
		ISendStallSource stallSource,
		ISessionControl session,
		ISteamService steam,
		ISessionNotices notices,
		ILocalizationService localization,
		ILogger<PeerSendStallWatchdog> log)
	{
		_stallSource = stallSource;
		_session = session;
		_steam = steam;
		_notices = notices;
		_localization = localization;
		_log = log;
		_stallSource.PeerSendStalled += OnPeerSendStalled;
		_session.SessionEnded += ResetSessionState;
	}

	void ICuoService.Initialize()
	{
	}

	void ICuoService.Start()
	{
	}

	/// <summary>Nothing per frame: the transport raises the stall edge on its own tick,
	/// this class only answers it. It is a lifecycle service so the container builds
	/// it (and its subscription) exactly once at startup.</summary>
	void ICuoService.Update()
	{
	}

	void ICuoService.Stop()
	{
	}

	public void Dispose()
	{
		_stallSource.PeerSendStalled -= OnPeerSendStalled;
		_session.SessionEnded -= ResetSessionState;
	}

	/// <summary>
	/// A finished session leaves nothing for the next one: the transport's refusal
	/// episodes, report windows and suppression counts are dropped here, so the first
	/// aggregate of a new session never counts the previous session's attempts.
	/// </summary>
	public void ResetSessionState() => _stallSource.ResetRefusals();

	private void OnPeerSendStalled(ulong steamId)
	{
		if (_session.Role == SessionRole.Host)
		{
			DropStalledMember(steamId);
			return;
		}

		if (_session.Role == SessionRole.Guest && steamId == _session.HostSteamId)
		{
			EndGuestSession(steamId);
		}
	}

	private void DropStalledMember(ulong steamId)
	{
		// Only a member the host still knows: an edge that arrives after the
		// member left (or for the host itself) has nothing to do.
		if (steamId == _session.LocalSteamId || !_session.TryGetMember(steamId, out var member))
		{
			return;
		}

		var name = member.DisplayName.Length > 0 ? member.DisplayName : _steam.GetPersonaName(steamId);
		_log.LogWarning("Dropping {Peer} ({Name}) from the session: {Reason}.", steamId, name, StallReason);
		if (_session.KickMember(steamId, StallReason))
		{
			_notices.Publish(_localization.Format("hud.peer_send_stalled", name));
		}
	}

	private void EndGuestSession(ulong hostId)
	{
		_log.LogWarning(
			"The host {Host} kept refusing our sends for the stall bound — ending the session locally.",
			hostId);
		_notices.Publish(_localization.T("hud.send_stalled"));
		_session.EndSession();
	}
}
