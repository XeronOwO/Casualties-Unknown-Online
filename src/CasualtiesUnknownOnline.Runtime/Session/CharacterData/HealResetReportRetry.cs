using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using CasualtiesUnknownOnline.Runtime.Time;
using Microsoft.Extensions.Logging;

namespace CasualtiesUnknownOnline.Runtime.Session.CharacterData;

/// <summary>
/// The reset result's send retry. The transport can refuse a send LOCALLY — its per-peer congestion
/// gate answers "suppressed" before anything reaches Steam (any message type, reliable or not) — and
/// this event is the only carrier of the three values the host owns: the ordinary character report
/// preserves them, so a send that never happened would silently reproduce the very defect the event
/// exists to fix. A refused send is not a lost race (nothing left the process), so the result is
/// re-offered on the character report cadence until the transport accepts it or the attempt budget is
/// spent, and every step is logged. The retry is dropped with the body/session it belongs to
/// (<see cref="Clear"/>), so a result captured from one body can never be written onto a later one.
///
/// <para>
/// A send the transport ACCEPTED rides Steam's reliable channel; a loss after that is the session's
/// (peer gone), and the host's own instruction expiry is where it surfaces. The budget is a named
/// limit, not a delivery guarantee: a peer that stays congested for the whole budget leaves the
/// target reverted until the next reset, and the ERROR line says so.
/// </para>
/// </summary>
internal sealed class HealResetReportRetry(
	ICharacterDataControl characters,
	ITimeSource time,
	ILogger log)
{
	/// <summary>How many report-cadence attempts one applied result gets before the run gives up and names it.</summary>
	internal const int MaxAttempts = 5;

	/// <summary>One retry per character report interval — the same 1 Hz the ordinary snapshot uses.</summary>
	internal const int RetryIntervalMs = 1000;

	private readonly ICharacterDataControl _characters = characters;
	private readonly ITimeSource _time = time;
	private readonly ILogger _log = log;
	private HealResetAppliedMsg? _pending;
	private int _attempts;
	private long _nextAttemptMs;

	/// <summary>Queue one applied reset result and offer it now; a frame the transport did not accept stays pending for <see cref="Tick"/>.</summary>
	public void Report(HealResetAppliedMsg msg)
	{
		_pending = msg;
		_attempts = 0;
		_nextAttemptMs = _time.NowMs;
		TrySend();
	}

	/// <summary>The character report pump's retry step: one attempt per interval while a result is pending.</summary>
	public void Tick()
	{
		var now = _time.NowMs;
		if (_pending is null)
		{
			return;
		}

		// A backwards clock (a TickCount wrap, a process suspended across one) would otherwise strand
		// the pending report until the clock passed the stale deadline; rebase instead.
		if (_nextAttemptMs - now > RetryIntervalMs)
		{
			_nextAttemptMs = now;
		}

		if (now < _nextAttemptMs)
		{
			return;
		}

		TrySend();
	}

	/// <summary>
	/// Drop a pending result whose body or session is gone: the send can no longer speak for it, and
	/// re-offering it later would write a stale body state onto a newer one.
	/// </summary>
	public void Clear()
	{
		if (_pending is null)
		{
			return;
		}

		_log.LogInformation("[HealReset] dropped the pending applied-reset report (instruction {InstructionId}): the local body or the session it belonged to is gone.",
			_pending.InstructionId);
		_pending = null;
		_attempts = 0;
	}

	private void TrySend()
	{
		var msg = _pending!;
		_attempts++;
		if (_characters.TrySendHealResetApplied(msg))
		{
			_log.LogInformation("[HealReset] reported the applied reset for {Peer} to the host (instruction {InstructionId}, attempt {Attempt}).",
				msg.OwnerSteamId, msg.InstructionId, _attempts);
			_pending = null;
			return;
		}

		_nextAttemptMs = _time.NowMs + RetryIntervalMs;
		if (_attempts >= MaxAttempts)
		{
			_log.LogError("[HealReset] the applied reset report (instruction {InstructionId}) was not accepted after {Attempts} attempts; the host keeps its previous values until the next reset.",
				msg.InstructionId, _attempts);
			_pending = null;
			return;
		}

		_log.LogWarning("[HealReset] the transport did not accept the applied reset report (instruction {InstructionId}, attempt {Attempt}); retrying on the report cadence.",
			msg.InstructionId, _attempts);
	}
}
