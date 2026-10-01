using System;
using CasualtiesUnknownOnline.Abstractions;
using CasualtiesUnknownOnline.Runtime.Localization;
using CasualtiesUnknownOnline.Runtime.Session.NetworkTraffic;
using CasualtiesUnknownOnline.Runtime.Time;
using Microsoft.Extensions.Logging;

namespace CasualtiesUnknownOnline.Runtime.Session;

/// <summary>
/// Guest side of the send-stall policy: a guest that has received no frame from
/// the host for the silence bound ends its own session instead of sitting in a
/// world that stopped moving. Any frame counts — the host pings every member on
/// a 5 s cadence and streams while in world, so 15 s of complete silence is not
/// a legitimate game state.
///
/// <para>
/// The judgement is local by design: the guest cannot know why the host went
/// quiet (a stalled host, a dropped member, a refusing link), but it can know
/// that it did, and it must not depend on a message that by definition cannot
/// arrive. The counter starts when the session becomes active, and a session
/// reset clears the receive history it reads, so no stale silence crosses
/// sessions.
/// </para>
/// </summary>
internal sealed class GuestHostSilenceWatchdog(
	ISessionControl session,
	NetworkTrafficMonitor traffic,
	ITimeSource time,
	ISessionNotices notices,
	ILocalizationService localization,
	ILogger<GuestHostSilenceWatchdog> log) : ICuoService
{
	/// <summary>Three host ping intervals: the point at which "no frame at all" stops being a quiet moment.</summary>
	internal const long DefaultSilenceMs = 15_000;

	private bool _armed;
	private long _armedAtMs;

	void ICuoService.Update()
	{
		if (session.Role != SessionRole.Guest || !session.SessionActive || session.HostSteamId == 0)
		{
			_armed = false;
			return;
		}

		var nowMs = time.NowMs;
		if (!_armed)
		{
			_armed = true;
			_armedAtMs = nowMs;
		}

		var lastFrameMs = traffic.TryGetLastReceiveMs(session.HostSteamId, out var receivedAtMs)
			? receivedAtMs
			: _armedAtMs;
		var silentMs = nowMs - lastFrameMs;
		if (silentMs < DefaultSilenceMs)
		{
			return;
		}

		log.LogWarning(
			"No frame from the host {Host} for {SilentMs} ms — ending the session locally.",
			session.HostSteamId,
			silentMs);

		// Disarm before acting: EndSession tears the session down, and a re-armed
		// watchdog must start its next window from the next session's first frame.
		_armed = false;
		notices.Publish(localization.T("hud.host_silent"));
		session.EndSession();
	}

	void ICuoService.Initialize()
	{
	}

	void ICuoService.Start()
	{
	}

	void ICuoService.Stop()
	{
	}

	void IDisposable.Dispose()
	{
	}
}
