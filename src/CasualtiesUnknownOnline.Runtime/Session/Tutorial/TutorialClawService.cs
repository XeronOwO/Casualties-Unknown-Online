using System;
using CasualtiesUnknownOnline.Abstractions;
using CasualtiesUnknownOnline.Runtime.Configuration;
using CasualtiesUnknownOnline.Runtime.Protocol;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using CasualtiesUnknownOnline.Runtime.Time;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CasualtiesUnknownOnline.Runtime.Session.Tutorial;

/// <summary>
/// The tutorial-claw presentation stream (host-authoritative, reusing the
/// enemy-stream pattern): the Game Adapter publishes the host's
/// <c>TutorialHandler</c> claw state; this service broadcasts it at the
/// configured state-stream cadence (default 20 Hz, unreliable, seq-gated) to
/// in-world guests. A guest not running its own tutorial course uses it to
/// render the same claw flow as the host. No course/prop state travels in this
/// slice — per-side tutorial course state and per-player claw props remain by
/// design.
/// </summary>
public sealed class TutorialClawService : ICuoService, ITutorialClawControl
{
	private readonly ISessionControl _session;
	private readonly PacketSender _sender;
	private readonly ITimeSource _time;
	private readonly IOptionsMonitor<StateStreamOptions> _stateStreamOptions;
	private readonly ILogger<TutorialClawService> _log;

	private TutorialClawStateMsg? _latest;
	private uint _nextSeq; // host: the broadcast seq
	private uint _lastSeq; // guest: the last applied seq (unreliable-stream gate)
	private long _nextSendMs;

	public TutorialClawService(ISessionControl session, PacketSender sender, ITimeSource time,
		IOptionsMonitor<StateStreamOptions> stateStreamOptions, ILogger<TutorialClawService> log)
	{
		_session = session;
		_sender = sender;
		_time = time;
		_stateStreamOptions = stateStreamOptions;
		_log = log;
		_session.SessionEnded += OnSessionEnded;
	}

	public event Action<TutorialClawStateMsg>? TutorialClawStateReceived;

	public void PublishTutorialClawState(TutorialClawStateMsg msg)
	{
		if (_session.Role != SessionRole.Host || !_session.SessionActive)
		{
			return;
		}

		_latest = msg;
	}

	public void ClearTutorialClawState() => _latest = null;

	public void ApplyTutorialClawState(TutorialClawStateMsg msg)
	{
		// Unreliable stream: drop stale/duplicate snapshots (single source — the host).
		if (msg.Seq <= _lastSeq)
		{
			return;
		}

		_lastSeq = msg.Seq;
		TutorialClawStateReceived?.Invoke(msg);
	}

	void ICuoService.Initialize()
	{
	}

	void ICuoService.Start()
	{
	}

	void ICuoService.Update()
	{
		if (_session.Role != SessionRole.Host || !_session.SessionActive || _latest is null)
		{
			return;
		}

		var nowMs = _time.NowMs;
		if (nowMs < _nextSendMs)
		{
			return;
		}

		_nextSendMs = nowMs + (long)(_stateStreamOptions.CurrentValue.SendIntervalSeconds * 1000f);
		Broadcast();
	}

	void ICuoService.Stop()
	{
	}

	public void Dispose() => _session.SessionEnded -= OnSessionEnded;

	private void Broadcast()
	{
		var msg = _latest!;
		msg.Seq = ++_nextSeq;

		foreach (var member in _session.Members)
		{
			if (member.Handshaken && member.InWorld && member.SteamId != _session.LocalSteamId)
			{
				_sender.Send(member.SteamId, NetMsg.TutorialClawState, msg, reliable: false);
			}
		}

		_log.LogDebug("[TutorialClaw] published seq {Seq} at ({X:F1},{Y:F1}) -> ({CX:F1},{CY:F1}).",
			msg.Seq, msg.HandPosX, msg.HandPosY, msg.HandPosCurrentX, msg.HandPosCurrentY);
	}

	private void OnSessionEnded()
	{
		_latest = null;
		_lastSeq = 0;
		_nextSeq = 0;
	}
}
