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

	/// <summary>World-start parameters: set by the host at run start, by the world-params handler on the guest.</summary>
	public WorldStartParams? WorldParams { get; set; }

	/// <summary>Host: a guest reported damage (apply + relay). Guest: the host broadcast it.</summary>
	public event Action<NetVector2, float>? BlockDamagedReceived;

	public void FireBlockDamagedReceived(NetVector2 pos, float damage) =>
		BlockDamagedReceived?.Invoke(pos, damage);

	/// <summary>Guest: the host told us to enter the world (its params are already in hand).</summary>
	public event Action? WorldJoinReceived;

	public void FireWorldJoinReceived() => WorldJoinReceived?.Invoke();

	/// <summary>Guest: the host's authoritative block-state snapshot arrived (world entry).</summary>
	public event Action<IReadOnlyList<DamagedBlock>>? BlockStateReceived;

	public void FireBlockStateReceived(IReadOnlyList<DamagedBlock> blocks) => BlockStateReceived?.Invoke(blocks);

	/// <summary>Either side: a block was placed (report up / broadcast down share one message id).</summary>
	public event Action<ulong, int, int, ushort>? BlockPlacedReceived;

	public void FireBlockPlacedReceived(ulong sender, int x, int y, ushort block) =>
		BlockPlacedReceived?.Invoke(sender, x, y, block);

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
	/// Host side: tell the members to enter the world. Sent after the world
	/// params (the guest's run-start gate then always passes) — the host owns
	/// the timing: at handshake time when it is already in a world, and when it
	/// enters the world itself.
	/// </summary>
	public void SendWorldJoin()
	{
		if (!_session.SessionActive)
		{
			return;
		}

		var msg = new WorldJoinMsg();
		foreach (var member in _session.Members)
		{
			if (member.Handshaken)
			{
				_sender.Send(member.SteamId, NetMsg.WorldJoin, msg);
			}
		}

		_log.LogInformation("World join sent to {Members} members.", _session.Members.Count(m => m.Handshaken));
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
