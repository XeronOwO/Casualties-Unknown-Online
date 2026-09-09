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

	private long _armedMs;

	/// <summary>Host only: the gate was released (everyone started, or the 30 s fallback fired).</summary>
	private bool _released;

	internal void SetHostRunPending(bool pending) => HostRunPending = pending;

	/// <summary>Host only: everyone enters the world together — arm the gate (waits for every guest's InWorld, or 30 s). Returns whether anyone is being waited on.</summary>
	internal bool Arm()
	{
		if (_session.Role != SessionRole.Host || !_session.SessionActive)
		{
			return false;
		}

		_released = false;
		var waiting = _session.Members
			.Where(m => m.Handshaken && !m.InWorld && m.SteamId != _session.LocalSteamId)
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

		if (_armed is null)
		{
			if (_released)
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
		_armedMs = 0;
		_released = false;
	}

	private void SendWorldReady()
	{
		if (!_session.SessionActive)
		{
			return;
		}

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
