using System;
using System.Collections.Generic;
using System.Linq;
using CasualtiesUnknownOnline.Runtime.Protocol;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
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
public sealed class WorldService(ISessionControl session, PacketSender sender, ILogger<WorldService> log)
	: IWorldControl
{
	private readonly ISessionControl _session = session;
	private readonly PacketSender _sender = sender;
	private readonly ILogger<WorldService> _log = log;

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
			// at the host's own loading moment.
			_startGate = null;
			SendWorldReady();
			return false;
		}

		_startGate = waiting;
		_startGateArmedMs = Environment.TickCount;
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

		if (Environment.TickCount - _startGateArmedMs <= StartGateTimeoutMs)
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
		: Math.Max(0, StartGateTimeoutMs - (int)(Environment.TickCount - _startGateArmedMs));

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

	/// <summary>Host: a guest reported damage (apply + relay). Guest: the host broadcast it.</summary>
	public event Action<NetVector2, float>? BlockDamagedReceived;

	public void FireBlockDamagedReceived(NetVector2 pos, float damage) =>
		BlockDamagedReceived?.Invoke(pos, damage);

	/// <summary>Guest: the host told us to enter the world — isTutorial = follow StartTutorial (it nulls runSettings itself), else StartRun.</summary>
	public event Action<bool>? WorldJoinReceived;

	public void FireWorldJoinReceived(bool isTutorial) => WorldJoinReceived?.Invoke(isTutorial);

	/// <summary>Guest: the host's authoritative block-state snapshot arrived (world entry).</summary>
	public event Action<IReadOnlyList<DamagedBlock>>? BlockStateReceived;

	public event Action<float, float>? EarthquakeStartReceived;

	public void FireBlockStateReceived(IReadOnlyList<DamagedBlock> blocks) => BlockStateReceived?.Invoke(blocks);

	/// <summary>Either side: a block was placed (report up / broadcast down share one message id).</summary>
	public event Action<ulong, int, int, ushort>? BlockPlacedReceived;

	public void FireBlockPlacedReceived(ulong sender, int x, int y, ushort block) =>
		BlockPlacedReceived?.Invoke(sender, x, y, block);

	/// <summary>A player's attack damaged a building entity — apply the damage to the entity at Pos.</summary>
	public event Action<NetVector2, float>? BuildingEntityDamagedReceived;

	public void FireBuildingEntityDamagedReceived(NetVector2 pos, float damage) =>
		BuildingEntityDamagedReceived?.Invoke(pos, damage);

	/// <summary>A lockable entity was opened — apply the open (health = 0) to the entity at Pos.</summary>
	public event Action<NetVector2>? BuildingEntityOpenedReceived;

	public void FireBuildingEntityOpenedReceived(NetVector2 pos) =>
		BuildingEntityOpenedReceived?.Invoke(pos);

	/// <summary>
	/// Report a locally-opened lockable entity (instant-open/lockpick/keypad —
	/// all write health = 0 directly, Openable.cs:12 / LockpingMinigame.cs:129 /
	/// KeypadMinigame.cs:138): guest → host as a report (the host applies the
	/// open to its copy — which rolls the host-side drops — and relays), host →
	/// guest as a broadcast relay.
	/// </summary>
	public void SendBuildingEntityOpened(NetVector2 pos)
	{
		if (!_session.SessionActive)
		{
			return;
		}

		var msg = new BuildingEntityOpenedMsg { Position = pos.ToNetVector2Msg() };
		if (_session.Role == SessionRole.Host)
		{
			_session.Broadcast(NetMsg.BuildingEntityOpened, msg);
		}
		else
		{
			_sender.Send(_session.HostSteamId, NetMsg.BuildingEntityOpened, msg);
		}
	}

	/// <summary>
	/// Report a locally-performed player attack on a building entity (local
	/// compute): guest → host as a report (the host applies the damage to its
	/// own copy — which is what rolls the host-side entity drops — and relays),
	/// host → guest as a broadcast relay. The entity is identified by its world
	/// position (world entities are generated deterministically, so both sides
	/// have the same object at the same place).
	/// </summary>
	public void SendBuildingEntityDamaged(NetVector2 pos, float damage)
	{
		if (!_session.SessionActive)
		{
			return;
		}

		var msg = new BuildingEntityDamagedMsg
		{
			Position = pos.ToNetVector2Msg(),
			Damage = damage,
		};
		if (_session.Role == SessionRole.Host)
		{
			_session.Broadcast(NetMsg.BuildingEntityDamaged, msg);
		}
		else
		{
			_sender.Send(_session.HostSteamId, NetMsg.BuildingEntityDamaged, msg);
		}
	}

	/// <summary>
	/// Guest side: a block was placed locally (local compute) — report it to
	/// the host, which arbitrates (target must be air) and relays.
	/// </summary>
	public void SendBlockPlacedReport(int x, int y, ushort block)
	{
		if (!_session.SessionActive)
		{
			return;
		}

		_sender.Send(_session.HostSteamId, NetMsg.BlockPlaced,
			new BlockPlacedMsg { X = x, Y = y, Block = block });
	}

	/// <summary>Host side: broadcast a placed block (source excluded — it already applied locally).</summary>
	public void BroadcastBlockPlaced(ulong excludeSteamId, int x, int y, ushort block)
	{
		if (!_session.SessionActive)
		{
			return;
		}

		var msg = new BlockPlacedMsg { X = x, Y = y, Block = block };
		_session.BroadcastExcept(excludeSteamId, NetMsg.BlockPlaced, msg);
	}

	public void BroadcastEarthquakeStart(float duration, float nextDelay)
	{
		if (_session.Role != SessionRole.Host || !_session.SessionActive)
		{
			return;
		}

		_session.Broadcast(NetMsg.EarthquakeStart, new EarthquakeStartMsg { Duration = duration, NextDelay = nextDelay });
	}

	public void FireEarthquakeStartReceived(float duration, float nextDelay)
	{
		_log.LogInformation("Earthquake started ({Duration:F1}s, next in {NextDelay:F0}s) — showing the effect, re-aligning the quake timer.", duration, nextDelay);
		EarthquakeStartReceived?.Invoke(duration, nextDelay);
	}

	/// <summary>
	/// Host only: a block now deviates from its generated baseline (mined,
	/// destroyed, built — the SetBlock write path, which damage application
	/// and earthquakes also go through) — upsert it into the difference table.
	/// </summary>
	public void ReportBlockState(int x, int y, ushort block)
	{
		if (_session.Role != SessionRole.Host || !_session.SessionActive)
		{
			return;
		}

		if (_damagedBlocks.Count >= MaxDamagedBlocks && !_damagedBlocks.ContainsKey((x, y)))
		{
			return; // cap reached — stop tracking new entries rather than grow unbounded
		}

		_damagedBlocks[(x, y)] = block;
	}

	/// <summary>Host only: a block was restored to its generated baseline — it is no longer part of the difference.</summary>
	public void RemoveBlockState(int x, int y)
	{
		if (_session.Role != SessionRole.Host || !_session.SessionActive)
		{
			return;
		}

		_damagedBlocks.Remove((x, y));
	}

	/// <summary>Host only: a new world layer is generating — the table starts empty again.</summary>
	public void ResetDamagedBlocks() => _damagedBlocks.Clear();

	/// <summary>Host only: send the full damage table to one member (on its world entry).</summary>
	public void SendBlockStateSnapshot(ulong targetSteamId)
	{
		if (_session.Role != SessionRole.Host || _damagedBlocks.Count == 0)
		{
			return;
		}

		var msg = new BlockStateMsg
		{
			Blocks = [.. _damagedBlocks.Select(kv => new BlockStateEntryMsg { X = kv.Key.Item1, Y = kv.Key.Item2, Block = kv.Value })],
		};
		_sender.Send(targetSteamId, NetMsg.WorldBlockState, msg);
		_log.LogInformation("Sent block-state snapshot ({Count} blocks) to {Peer}.", _damagedBlocks.Count, targetSteamId);
	}

	/// <summary>
	/// Host side: tell the members to enter the world. Sent at run-start entry
	/// (the host clicks start — the guest starts its transition immediately,
	/// BEFORE the world params exist; the guest's generation boundary waits for
	/// them) and at handshake time when the host is already in a world (there
	/// the params arrive first, ordered before the join).
	/// </summary>
	public void SendWorldJoin(bool isTutorial)
	{
		if (!_session.SessionActive)
		{
			return;
		}

		var msg = new WorldJoinMsg { IsTutorial = isTutorial };
		foreach (var member in _session.Members)
		{
			if (member.Handshaken)
			{
				_sender.Send(member.SteamId, NetMsg.WorldJoin, msg);
			}
		}

		_log.LogInformation("World join sent to {Members} members (tutorial: {Tutorial}).",
			_session.Members.Count(m => m.Handshaken), isTutorial);
	}

	/// <summary>Host side: capture and publish world-start parameters (run start).</summary>
	public void PublishWorldParams(WorldStartParams parameters)
	{
		WorldParams = parameters; // the handshake handlers read this when acking a new member
		if (!_session.SessionActive)
		{
			return;
		}

		var msg = parameters.ToWorldStartParamsMsg();
		var members = _session.Members.Where(m => m.Handshaken).ToList();
		foreach (var member in members)
		{
			_sender.Send(member.SteamId, NetMsg.WorldStartParams, msg);
		}

		_log.LogInformation("Published world params ({StateBytes} bytes) to {Members} members.",
			parameters.RandomState.Length, members.Count);
	}

	/// <summary>
	/// Report a locally-performed block damage (local compute): guest → host as
	/// a report (the host arbitrates and relays), host → broadcast to all synced
	/// members (the source excluded on relay — it already applied locally).
	/// </summary>
	public void SendBlockDamaged(NetVector2 worldPos, float damage)
	{
		if (!_session.SessionActive)
		{
			return;
		}

		var msg = new BlockDamagedMsg
		{
			Position = worldPos.ToNetVector2Msg(),
			Damage = damage,
		};
		if (_session.Role == SessionRole.Host)
		{
			_session.Broadcast(NetMsg.BlockDamaged, msg);
		}
		else
		{
			_sender.Send(_session.HostSteamId, NetMsg.BlockDamaged, msg);
		}
	}
}
