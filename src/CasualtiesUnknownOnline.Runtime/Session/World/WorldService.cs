using System;
using System.Collections.Generic;
using System.Linq;
using CasualtiesUnknownOnline.Runtime.Protocol;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using CasualtiesUnknownOnline.Runtime.Time;
using Microsoft.Extensions.Logging;

namespace CasualtiesUnknownOnline.Runtime.Session.World;

/// <summary>
/// The world domain (world-defining state + world-change events): owns the
/// world-start parameters captured by the host at run start and applied by
/// guests before their own world generation, and shuttles block-damage reports
/// (local compute → report → host relay). Owns no session state — it reads the
/// member roster through <see cref="ISessionControl"/> and fans out with
/// <see cref="PacketSender"/>. No pump: it only reacts to calls and messages
/// (not an ICuoService, like CharacterDataStore).
/// </summary>
public sealed partial class WorldService : IWorldControl, IDisposable
{
	private readonly ISessionControl _session;
	private readonly PacketSender _sender;
	private readonly ITimeSource _time;
	private readonly ILogger<WorldService> _log;
	private readonly EntityEventChannel _eventChannel;
	private readonly TradeChannel _tradeChannel;
	private readonly SpeechChannel _speechChannel;
	private readonly BlockDamageRegistry _blockDamageRegistry;

	public WorldService(ISessionControl session, PacketSender sender, ITimeSource time,
		ILogger<WorldService> log, EntityEventChannel eventChannel,
		TradeChannel tradeChannel, SpeechChannel speechChannel,
		BlockDamageRegistry blockDamageRegistry)
	{
		_session = session;
		_sender = sender;
		_time = time;
		_log = log;
		_eventChannel = eventChannel;
		_tradeChannel = tradeChannel;
		_speechChannel = speechChannel;
		_blockDamageRegistry = blockDamageRegistry;

		// Session-scoped world state dies with the session: a lobby switch or
		// host exit must never leak a start gate, pending-run flag, params or
		// damage table into the next session. The host session survives a
		// guest leaving, so same-session reconnects keep their state.
		session.SessionEnded += OnSessionEnded;
	}

	/// <summary>
	/// Host-side block-difference table: block-space position → current block id,
	/// for every block whose state deviates from the generated baseline (the
	/// adapter's SetBlock hook diffs against the baseline snapshot and upserts
	/// here, or removes the entry when a block is restored to it). The table is
	/// exactly the "current vs baseline" difference — the full sync payload for
	/// late joiners. Mined, destroyed, built and reverted blocks all land here.
	/// </summary>
	private readonly Dictionary<(int, int), ushort> _damagedBlocks = [];

	/// <summary>Table cap — a fully-mined world would otherwise grow without bound.</summary>
	private const int MaxDamagedBlocks = 65536;

	/// <summary>
	/// Host only: a run is in progress but the host has not entered the world
	/// yet (click moment → world entry). A handshake during this window may
	/// follow immediately — waiting for the world-entry re-invite would start
	/// the guest's loading a whole host generation late.
	/// </summary>
	public bool HostRunPending { get; private set; }

	public void SetHostRunPending(bool pending) => HostRunPending = pending;

	/// <summary>Host only: the armed start gate — SteamIds still loading, armed at world entry. Everyone starts playing together (or after 30 s).</summary>
	private HashSet<ulong>? _startGate;
	private long _startGateArmedMs;

	/// <summary>
	/// Host only: the gate was released (everyone started, or the 30 s fallback
	/// fired). Distinguishes "never armed" (host still generating — an early
	/// InWorld report must NOT pass the member through; the host's arm checks
	/// everyone's InWorld itself) from "released" (game running — a later
	/// InWorld is a late joiner and passes directly).
	/// </summary>
	private bool _gateReleased;

	/// <summary>Start-gate fallback: force the start if a guest is still loading after this long.</summary>
	private const int StartGateTimeoutMs = 30_000;

	/// <summary>World-start parameters: set by the host at run start, by the world-params handler on the guest.</summary>
	public WorldStartParams? WorldParams { get; set; }

	/// <summary>Guest: the host released the start gate — start playing (or, for a late joiner, enter directly).</summary>
	public event Action? WorldReadyReceived;

	public void FireWorldReadyReceived() => WorldReadyReceived?.Invoke();

	/// <summary>
	/// Host only: arm the start gate at world entry — every handshaken guest
	/// still loading must report InWorld before anyone starts playing, or 30 s
	/// elapse (the slow ones finish on their own). Members already InWorld
	/// (they finished while the host was still generating — their early report
	/// arrived before the arm and was held) are not waited on. Returns whether
	/// anyone is being waited on (no guests: nothing to wait for).
	/// </summary>
	public bool StartStartGate()
	{
		if (_session.Role != SessionRole.Host || !_session.SessionActive)
		{
			return false;
		}

		_gateReleased = false;
		var waiting = _session.Members
			.Where(m => m.Handshaken && !m.InWorld && m.SteamId != _session.LocalSteamId)
			.Select(m => m.SteamId).ToHashSet();
		if (waiting.Count == 0)
		{
			// Everyone is already in the world (or there is no one) — no
			// waiting: release everyone right away so the game starts together
			// at the host's own loading moment. (Handshaken now means the
			// handshake completed end-to-end — HandshakeAckAck — so a member
			// whose ack never arrived is not waited on: the host starts, and
			// the guest enters as a late joiner once its connection completes.)
			if (_session.Members.Any(m => m.SteamId != _session.LocalSteamId))
			{
				_log.LogInformation("No confirmed members waiting — releasing the start gate immediately.");
			}

			_startGate = null;
			SendWorldReady();
			return false;
		}

		_startGate = waiting;
		_startGateArmedMs = _time.NowMs;
		_log.LogInformation("Start gate armed — waiting for {Count} member(s) to finish loading.", waiting.Count);
		return true;
	}

	/// <summary>
	/// Host only: a member finished loading. Gate armed → drop it from the
	/// wait list; when the list empties, release everyone at once. Gate never
	/// armed (the host is still generating) → hold the report: the host's arm
	/// checks every member's InWorld itself. Gate released (game running) →
	/// the late joiner enters directly.
	/// </summary>
	public void NotifyMemberInWorld(ulong steamId)
	{
		if (_session.Role != SessionRole.Host || !_session.SessionActive)
		{
			return;
		}

		if (_startGate is null)
		{
			if (_gateReleased)
			{
				SendWorldReadyTo(steamId); // late joiner: the game is running — pass it in directly
			}

			return; // never armed: the host's StartStartGate reads the member's InWorld itself
		}

		_startGate.Remove(steamId);
		if (_startGate.Count == 0)
		{
			_startGate = null;
			SendWorldReady();
			_log.LogInformation("Start gate released — everyone is in the world.");
		}
	}

	/// <summary>Host only: driver pump — the gate forces the start after 30 s (slow loaders finish on their own).</summary>
	public void MaybeForceStartGate()
	{
		if (_startGate is not { Count: > 0 })
		{
			return;
		}

		if (_time.NowMs - _startGateArmedMs <= StartGateTimeoutMs)
		{
			return;
		}

		_log.LogWarning("Start gate forced after {Timeout} s — still waiting for {Count} member(s); they join when they finish loading.",
			StartGateTimeoutMs / 1000, _startGate.Count);
		_startGate = null;
		_gateReleased = true;
		SendWorldReady();
	}

	/// <summary>Host only: true while the host itself must wait (frozen + overlay).</summary>
	public bool StartGateActive => _startGate is not null;

	/// <summary>Host only: milliseconds left until the gate force-releases (0 when not armed).</summary>
	public int StartGateRemainingMs => _startGate is null
		? 0
		: Math.Max(0, StartGateTimeoutMs - (int)(_time.NowMs - _startGateArmedMs));

	/// <summary>Host only: release the start gate to everyone.</summary>
	private void SendWorldReady()
	{
		if (!_session.SessionActive)
		{
			return;
		}

		_gateReleased = true;
		var msg = new WorldReadyMsg();
		foreach (var member in _session.Members)
		{
			if (member.Handshaken)
			{
				_sender.Send(member.SteamId, NetMsg.WorldReady, msg);
			}
		}
	}

	/// <summary>Host only: release one member (late joiner).</summary>
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
