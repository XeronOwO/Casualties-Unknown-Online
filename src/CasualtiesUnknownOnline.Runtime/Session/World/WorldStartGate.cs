using System;
using System.Collections.Generic;
using System.Linq;
using CasualtiesUnknownOnline.Runtime.Protocol;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using CasualtiesUnknownOnline.Runtime.Time;
using Microsoft.Extensions.Logging;

namespace CasualtiesUnknownOnline.Runtime.Session.World;

/// <summary>
/// The host start-gate lifecycle (extracted from <see cref="WorldService"/> at
/// the 600-line gate): the host's click moment arms the gate, every confirmed
/// member's InWorld report releases its slot, and a 30 s fallback releases the
/// gate even if a member is still loading (that member enters directly when it
/// finishes). The state belongs here — the armed member set, the arm timestamp
/// and the released flag are one lifecycle, not world-defining state.
/// </summary>
internal sealed class WorldStartGate(ISessionControl session, PacketSender sender, ITimeSource time, ILogger<WorldService> log)
{
	/// <summary>Start-gate fallback: force the start if a guest is still loading after this long.</summary>
	private const int TimeoutMs = 30_000;

	private readonly ISessionControl _session = session;
	private readonly PacketSender _sender = sender;
	private readonly ITimeSource _time = time;
	private readonly ILogger<WorldService> _log = log;

	/// <summary>Host only: a run is in progress but the host has not entered the world yet (click moment → world entry). A handshake during this window may follow immediately.</summary>
	internal bool HostRunPending { get; private set; }

	/// <summary>Host only: the armed start gate — SteamIds still loading, armed at world entry.</summary>
	private HashSet<ulong>? _armed;

	/// <summary>
	/// Host only: the members a layer descent was announced to — expected back in the new world even
	/// while their presence table still says InWorld (their own exit report has not arrived yet). A
	/// member that reports InWorld removes itself, so a member that finished before the host entered is
	/// NOT waited on; the set only covers the stale-InWorld window and a member whose descent edge was
	/// lost (it follows the ordinary menu re-entry instead).
	/// </summary>
	private HashSet<ulong>? _descentPending;

	private long _armedMs;

	/// <summary>Host only: the gate was released (everyone started, or the 30 s fallback fired).</summary>
	private bool _released;

	internal void SetHostRunPending(bool pending)
	{
		HostRunPending = pending;
		if (!pending)
		{
			return;
		}

		// A new run begins at the host's click: the previous run's verdict is not this one's. A
		// stale release would pass a member whose world came up before the host's own entry (the new
		// gate arms there, and until it does this run has no verdict of its own), and a stale armed
		// set would hold it for slots that belong to the run being left.
		_released = false;
		_armed = null;
	}

	/// <summary>Host only: a layer descent was announced to these members — the next gate must wait for them even if their presence still reads InWorld, and the PREVIOUS run's release must not pass one of them before this descent's gate arms.</summary>
	internal void ExpectDescent(IReadOnlyCollection<ulong> members)
	{
		_released = false;
		_descentPending = [.. members];
		_log.LogInformation("Start gate expects {Count} member(s) back from the layer descent.", members.Count);
	}

	/// <summary>Host only: everyone enters the world together — arm the gate (waits for every guest's InWorld, or 30 s). Returns whether anyone is being waited on.</summary>
	internal bool Arm()
	{
		if (_session.Role != SessionRole.Host || !_session.SessionActive)
		{
			return false;
		}

		_released = false;
		var descentPending = _descentPending;
		_descentPending = null; // consumed by this arm: from here the armed set is the wait
		var waiting = _session.Members
			.Where(m => m.Handshaken && m.SteamId != _session.LocalSteamId
				&& (!m.InWorld || (descentPending?.Contains(m.SteamId) ?? false)))
			.Select(m => m.SteamId).ToHashSet();
		if (waiting.Count == 0)
		{
			if (_session.Members.Any(m => m.SteamId != _session.LocalSteamId))
			{
				_log.LogInformation("No confirmed members waiting — releasing the start gate immediately.");
			}

			_armed = null;
			SendWorldReady();
			return false;
		}

		_armed = waiting;
		_armedMs = _time.NowMs;
		_log.LogInformation("Start gate armed — waiting for {Count} member(s) to finish loading.", waiting.Count);
		return true;
	}

	/// <summary>Host only: a member finished loading (InWorld) — release the gate when all are in, or let a late joiner pass directly.</summary>
	internal void NotifyMemberInWorld(ulong steamId)
	{
		if (_session.Role != SessionRole.Host || !_session.SessionActive)
		{
			return;
		}

		// A descenting member that is already back in the new world must not be waited on: its arrival
		// removes it from the expectation set even when the gate has not armed yet.
		_descentPending?.Remove(steamId);

		if (_armed is null)
		{
			if (LateJoinerPasses)
			{
				SendWorldReadyTo(steamId);
			}

			return;
		}

		_armed.Remove(steamId);
		if (_armed.Count == 0)
		{
			_armed = null;
			SendWorldReady();
			_log.LogInformation("Start gate released — everyone is in the world.");
		}
	}

	/// <summary>
	/// Host only: a member re-asserted its InWorld state (its readiness window has not
	/// seen the host's answer). Re-answer with the gate's CURRENT verdict — a released
	/// gate: the same targeted WorldReady a late joiner gets; still armed: nothing,
	/// because the release broadcast covers every handshaken member; no gate over a run
	/// that is already playing (see <see cref="LateJoinerPasses"/>): the same targeted
	/// WorldReady, because its first copy may be the frame the lazy session swallowed.
	/// Never releases the gate — a repeat is not a new arrival.
	/// </summary>
	internal void AnswerRepeatInWorld(ulong steamId)
	{
		if (_session.Role != SessionRole.Host || !_session.SessionActive || _armed is not null || !LateJoinerPasses)
		{
			return;
		}

		// The same message as the late-joiner pass; the log line differs because this is
		// an ANSWER to a re-asserted report, not a member arriving for the first time.
		_sender.Send(steamId, NetMsg.WorldReady, new WorldReadyMsg());
		_log.LogInformation("Start-gate state re-answered to {Peer} — the member passes: the gate is released, or this run never armed one.", steamId);
	}

	/// <summary>
	/// The gate's verdict for a member that is already in the world. A RELEASED gate
	/// lets it in; so does a run whose gate was never armed for it — the host entered
	/// the world before this session existed (it played solo and opened the lobby from
	/// inside the running world), so no release was ever broadcast and the arriving
	/// member would wait out its whole 60 s valve and be returned to the menu (batch
	/// 20261021-e, session B). The host being in the world is the fact that makes the
	/// wait meaningless: a gate that IS armed over the run still holds its members (the
	/// callers check <c>_armed</c> first), and a host still entering a fresh layer has
	/// LocalInWorld false, so the gate that arms at its own entry still covers that
	/// entry's members.
	/// <para>
	/// A DESCENT expectation suspends the late-joiner pass entirely: between the host's
	/// edge and its new-world entry the host may still read LocalInWorld (its own exit
	/// report has not run yet), and the previous run's release is not this descent's
	/// verdict — a member that finishes its new layer first must wait for the descent's
	/// gate, not be passed by either rule.
	/// </para>
	/// </summary>
	private bool LateJoinerPasses => _descentPending is null && (_released || (_armed is null && _session.LocalInWorld));

	/// <summary>Host only: the gate is armed (the host is waiting too) — the driver pumps this for the 30 s fallback.</summary>
	internal void PumpTimeout()
	{
		if (_armed is not { Count: > 0 })
		{
			return;
		}

		if (_time.NowMs - _armedMs <= TimeoutMs)
		{
			return;
		}

		_log.LogWarning("Start gate forced after {Timeout} s — still waiting for {Count} member(s); they join when they finish loading.",
			TimeoutMs / 1000, _armed.Count);
		_armed = null;
		_released = true;
		SendWorldReady();
	}

	internal bool Active => _armed is not null;

	internal int RemainingMs => _armed is null
		? 0
		: Math.Max(0, TimeoutMs - (int)(_time.NowMs - _armedMs));

	/// <summary>The session ended — the next run arms a fresh gate.</summary>
	internal void Reset()
	{
		HostRunPending = false;
		_armed = null;
		_descentPending = null;
		_armedMs = 0;
		_released = false;
	}

	private void SendWorldReady()
	{
		if (!_session.SessionActive)
		{
			return;
		}

		_descentPending = null;
		_released = true;
		var msg = new WorldReadyMsg();
		foreach (var member in _session.Members)
		{
			if (member.Handshaken)
			{
				_sender.Send(member.SteamId, NetMsg.WorldReady, msg);
			}
		}
	}

	private void SendWorldReadyTo(ulong steamId)
	{
		if (!_session.SessionActive)
		{
			return;
		}

		_sender.Send(steamId, NetMsg.WorldReady, new WorldReadyMsg());
		_log.LogInformation("Start gate pass — {Peer} enters directly (game already running).", steamId);
	}
}
