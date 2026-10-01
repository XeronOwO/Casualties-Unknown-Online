using System;
using System.Runtime.InteropServices;
using CasualtiesUnknownOnline.Abstractions;
using CasualtiesUnknownOnline.Runtime.Steam;
using CasualtiesUnknownOnline.Runtime.Time;
using Microsoft.Extensions.Logging;
using Steamworks;

namespace CasualtiesUnknownOnline.Runtime.Networking;

/// <summary>
/// MVP transport over ISteamNetworkingMessages (reliable + unreliable
/// messages, no connection handles). Single channel (0) for now; the
/// INetworkTransport surface it implements is shared with the test suite's
/// FakeTransport (the second transport the abstraction was waiting for).
///
/// <para>
/// The Steam calls live behind <see cref="ISteamSendChannel"/>, and the
/// send-failure handling lives in <see cref="PeerSendRefusalPolicy"/>: a peer
/// whose queue is full is gated with exponential backoff, its refusals are
/// aggregated instead of logged once per attempt — the shape that grew the
/// host's log to 1.3 GB in Run E — and a peer that keeps refusing past the
/// stall bound raises <see cref="ISendStallSource.PeerSendStalled"/> for the
/// session layer to drop it.
/// </para>
/// </summary>
public sealed class SteamTransport : ICuoService, INetworkTransport, ISendStallSource
{
	private const int MaxMessagesPerPoll = 32;

	private readonly ISteamService _steam;
	private readonly ISteamSendChannel _sendChannel;
	private readonly ITimeSource _time;
	private readonly PeerSendRefusalPolicy _refusals = new();
	private readonly ILogger<SteamTransport> _log;
	private readonly IntPtr[] _receiveBuffer = new IntPtr[MaxMessagesPerPoll];

	internal SteamTransport(ISteamService steam, ISteamSendChannel sendChannel, ITimeSource time, ILogger<SteamTransport> log)
	{
		_steam = steam;
		_sendChannel = sendChannel;
		_time = time;
		_log = log;
	}

	/// <summary>Raised on the Unity main thread via <see cref="Poll"/>.</summary>
	public event Action<ulong, byte[]>? MessageReceived;

	private event Action<ulong>? _peerSendStalled;

	event Action<ulong>? ISendStallSource.PeerSendStalled
	{
		add => _peerSendStalled += value;
		remove => _peerSendStalled -= value;
	}

	void ISendStallSource.ResetRefusals() => _refusals.Reset();

	public bool SendTo(ulong steamId, byte[] data, bool reliable)
	{
		if (!_steam.IsInitialized)
		{
			return false;
		}

		var nowMs = _time.NowMs;
		if (!_refusals.TryBeginSend(steamId, nowMs))
		{
			// The peer's queue is still refusing: no Steam call and no log line
			// for this attempt. The suppressed attempt is counted and shows up in
			// the next aggregate report instead.
			return false;
		}

		var result = _sendChannel.Send(steamId, data, reliable);
		if (result == EResult.k_EResultOK)
		{
			if (_refusals.RecordSuccess(steamId, nowMs) is { } recovery)
			{
				_log.LogInformation(
					"Sends to {Peer} recovered after {Seconds:F1} s (refused {Refused}, suppressed {Suppressed}).",
					steamId,
					recovery.CongestedForMs / 1000d,
					recovery.RefusedAttempts,
					recovery.SuppressedAttempts);
			}

			return true;
		}

		// The gate decision is made from the result alone — reading the peer's
		// session info is a native call, and it only happens when a diagnostic
		// line is actually due (the first failure of a report window).
		var gatingKind = SteamSendFailureClassifier.ClassifyResult(result);
		if (_refusals.RecordFailure(steamId, gatingKind, nowMs))
		{
			var session = _sendChannel.DescribeSession(steamId);
			LogSendDiagnostics(steamId, result, SteamSendFailureClassifier.Classify(result, session.EndReason), session);
		}

		return false;
	}

	// Diagnose why a P2P send failed: session connection state, end reason and
	// debug string, plus the Steam Datagram Relay (SDR) availability. Only the
	// first failure of a refusal report window reaches this method; every repeat
	// is aggregated into the periodic line instead of printing this one again.
	private void LogSendDiagnostics(ulong steamId, EResult result, SteamSendFailureKind kind, SteamPeerSessionInfo session)
	{
		_log.LogWarning(
			$"SendMessageToUser to {steamId} failed: {result} ({kind}); " +
			$"session state: {session.State}, end reason: {session.EndReason}, debug: \"{session.Debug}\"; " +
			$"{SteamSendFailureClassifier.Remediation(kind)}");

		var relay = _sendChannel.DescribeRelay();
		if (relay.Current)
		{
			_log.LogWarning($"SDR: any-relay avail: {relay.AnyRelay}, network-config avail: {relay.NetworkConfig}, debug: \"{relay.Debug}\"");
		}
	}

	/// <summary>
	/// Flushes the refusal aggregates due this frame: one line per peer per
	/// report window, plus the stall escalation for a peer whose queue has
	/// refused everything for the stall bound. Internal so the tests can pump it
	/// without the native receive path.
	/// </summary>
	internal void FlushSendReports()
	{
		var nowMs = _time.NowMs;
		foreach (var report in _refusals.CollectDueReports(nowMs))
		{
			_log.LogWarning(
				"SendMessageToUser to {Peer}: {Refused} refusal(s) and {Suppressed} suppressed attempt(s) over {WindowMs} ms (queue full for {CongestedMs} ms).",
				report.PeerId,
				report.RefusedAttempts,
				report.SuppressedAttempts,
				report.WindowMs,
				report.CongestedForMs);
		}

		foreach (var stall in _refusals.CollectStallEscalations(nowMs))
		{
			_log.LogError(
				"Peer {Peer} refused every send for {Seconds:F1} s ({Refused} refusal(s)) — the session layer must drop it.",
				stall.PeerId,
				stall.CongestedForMs / 1000d,
				stall.RefusedAttempts);
			_peerSendStalled?.Invoke(stall.PeerId);
		}
	}

	/// <summary>Drains incoming messages. Must run on the Unity main thread each frame.</summary>
	public void Poll()
	{
		if (!_steam.IsInitialized)
		{
			return;
		}

		int count;
		while ((count = SteamNetworkingMessages.ReceiveMessagesOnChannel(0, _receiveBuffer, _receiveBuffer.Length)) > 0)
		{
			for (var i = 0; i < count; i++)
			{
				var message = _receiveBuffer[i];
				try
				{
					HandleMessage(message);
				}
				catch (Exception ex)
				{
					// One malformed/unappliable packet must never kill the rest of
					// the batch: Steam removes the whole received array from its
					// queue up front, so a throw here would silently lose every
					// message after the bad one. Observed 2026-08-15: an enemy
					// snapshot's null-prefab materialization threw before the
					// WorldReady that followed it in the same poll batch, and the
					// guest sat at the start gate for the full 60 s timeout.
					_log.LogError(ex, "SteamTransport: one received message failed — continuing with the rest of the batch.");
				}
				finally
				{
					SteamNetworkingMessage_t.Release(message);
				}
			}
		}
	}

	private void HandleMessage(IntPtr messagePtr)
	{
		var message = SteamNetworkingMessage_t.FromIntPtr(messagePtr);

		var data = new byte[message.m_cbSize];
		Marshal.Copy(message.m_pData, data, 0, message.m_cbSize);

		var sender = message.m_identityPeer.GetSteamID64();
		MessageReceived?.Invoke(sender, data);
	}

	void ICuoService.Initialize()
	{
	}

	void ICuoService.Start()
	{
	}

	void ICuoService.Update()
	{
		Poll();
		FlushSendReports();
	}

	void ICuoService.Stop()
	{
	}

	void IDisposable.Dispose()
	{
	}
}
