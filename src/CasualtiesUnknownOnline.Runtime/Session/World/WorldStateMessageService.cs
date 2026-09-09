using System;
using System.Collections.Generic;
using System.Linq;
using CasualtiesUnknownOnline.Runtime.Protocol;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using Microsoft.Extensions.Logging;

namespace CasualtiesUnknownOnline.Runtime.Session.World;

/// <summary>
/// The world block/building/state message-flow surface. It owns the
/// block-difference table, the radiation-line snapshot source, world-start
/// parameters and the message/event plumbing for world joins, block damage,
/// building-entity damage/open, earthquakes, keypads/geysers and block-state
/// backfill. The start-gate lifecycle stays in <see cref="WorldService"/>.
/// </summary>
internal sealed class WorldStateMessageService(
	ISessionControl session,
	PacketSender sender,
	ILogger<WorldService> log,
	EntityEventChannel eventChannel,
	BlockDamageRegistry blockDamageRegistry)
{
	private readonly ISessionControl _session = session;
	private readonly PacketSender _sender = sender;
	private readonly ILogger<WorldService> _log = log;
	private readonly EntityEventChannel _eventChannel = eventChannel;
	private readonly BlockDamageRegistry _blockDamageRegistry = blockDamageRegistry;

	/// <summary>
	/// Host-side block-difference table: block-space position → current block id,
	/// for every block whose state deviates from the generated baseline. Mined,
	/// destroyed, built and reverted blocks all land here.
	/// </summary>
	private readonly Dictionary<(int, int), ushort> _damagedBlocks = [];

	/// <summary>Table cap — a fully-mined world would otherwise grow without bound. Internal so the pending-report table's own cap can be asserted against it.</summary>
	internal const int MaxDamagedBlocks = 65536;

	/// <summary>
	/// Guest-side table of locally-applied block mutations whose report the host
	/// has not answered yet (sync-coverage audit W1): the host→guest direction
	/// heals a lost relay with the absolute table above, the guest→host report
	/// had no recovery at all. Populated by <see cref="SendBlockPlacedReport"/>,
	/// re-reported by <see cref="ResendPendingBlockReports"/> and dropped when the
	/// host answers for the cell (its relay echo or its correction) or when a new
	/// world/layer baseline is applied. The absolute snapshot and the world-entry
	/// completion marker deliberately do NOT clear it — a reconnect-while-in-world
	/// keeps the guest's local mutations, and re-reporting them is the recovery.
	/// </summary>
	private readonly PendingBlockReportTable _pendingBlockReports = new();

	private bool _pendingReportOverflowLogged;

	public WorldStartParams? WorldParams { get; set; }

	public RadiationLineStateMsg? RadiationLineState { get; private set; }

	// ---- Block damage (late-joiner backfill) ----

	public event Action<IReadOnlyList<BlockDamageEntryMsg>>? BlockDamageSnapshotReceived;

	public void FireBlockDamageSnapshotReceived(IReadOnlyList<BlockDamageEntryMsg> entries) =>
		BlockDamageSnapshotReceived?.Invoke(entries);

	public void ReportBlockDamage(int x, int y, float damage) => _blockDamageRegistry.Report(x, y, damage);

	public void RemoveBlockDamage(int x, int y) => _blockDamageRegistry.Remove(x, y);

	public void SendBlockDamageSnapshot(ulong targetSteamId) => _blockDamageRegistry.SendSnapshot(targetSteamId);

	// ---- World message flow ----

	public event Action<ulong, NetVector2, float, bool, IReadOnlyList<BlockDropEntryMsg>?, IReadOnlyList<TrapDropEntryMsg>?>? BlockDamagedReceived;

	public void FireBlockDamagedReceived(ulong sender, NetVector2 pos, float damage, bool metalBonus, IReadOnlyList<BlockDropEntryMsg>? drops, IReadOnlyList<TrapDropEntryMsg>? buildingDrops) =>
		BlockDamagedReceived?.Invoke(sender, pos, damage, metalBonus, drops, buildingDrops);

	public event Action<bool>? WorldJoinReceived;

	public void FireWorldJoinReceived(bool isTutorial) => WorldJoinReceived?.Invoke(isTutorial);

	public event Action? WorldSnapshotCompleteReceived;

	public void FireWorldSnapshotCompleteReceived() => WorldSnapshotCompleteReceived?.Invoke();

	public void SendWorldSnapshotComplete(ulong targetSteamId)
	{
		if (_session.Role != SessionRole.Host || targetSteamId == 0)
		{
			return;
		}

		_sender.Send(targetSteamId, NetMsg.WorldSnapshotComplete, new WorldSnapshotCompleteMsg());
	}

	public event Action<IReadOnlyList<DamagedBlock>>? BlockStateReceived;

	public event Action<float, float>? EarthquakeStartReceived;

	public event Action<IReadOnlyList<KeypadEntryMsg>>? KeypadCodeReceived;

	public void FireKeypadCodeReceived(IReadOnlyList<KeypadEntryMsg> codes) => KeypadCodeReceived?.Invoke(codes);

	public event Action<IReadOnlyList<GeyserStateEntryMsg>>? GeyserStateReceived;

	public void FireGeyserStateReceived(IReadOnlyList<GeyserStateEntryMsg> geysers) => GeyserStateReceived?.Invoke(geysers);

	public void SendGeyserStateSnapshot(IReadOnlyList<GeyserStateEntryMsg> geysers)
	{
		if (_session.Role != SessionRole.Host || !_session.SessionActive || geysers.Count == 0)
		{
			return;
		}

		var msg = new GeyserStateSnapshotMsg { Geysers = [.. geysers] };
		foreach (var member in _session.Members)
		{
			if (member.Handshaken)
			{
				_sender.Send(member.SteamId, NetMsg.GeyserStateSnapshot, msg);
			}
		}
	}

	public void SendKeypadCodes(IReadOnlyList<KeypadEntryMsg> codes)
	{
		if (_session.Role != SessionRole.Host || !_session.SessionActive || codes.Count == 0)
		{
			return;
		}

		var msg = new KeypadCodeMsg { Codes = [.. codes] };
		foreach (var member in _session.Members)
		{
			if (member.Handshaken)
			{
				_sender.Send(member.SteamId, NetMsg.KeypadCode, msg);
			}
		}
	}

	public void FireBlockStateReceived(IReadOnlyList<DamagedBlock> blocks) => BlockStateReceived?.Invoke(blocks);

	public event Action<ulong, int, int, ushort>? BlockPlacedReceived;

	public void FireBlockPlacedReceived(ulong sender, int x, int y, ushort block) =>
		OnBlockPlacedReceived(sender, x, y, block);

	public event Action<NetVector2, float, bool, bool>? BuildingEntityDamagedReceived;

	public void FireBuildingEntityDamagedReceived(NetVector2 pos, float damage, bool playHitSound, bool playHitFlash) =>
		BuildingEntityDamagedReceived?.Invoke(pos, damage, playHitSound, playHitFlash);

	public event Action<NetVector2>? BuildingEntityOpenedReceived;

	public void FireBuildingEntityOpenedReceived(NetVector2 pos)
	{
		if (_session.Role == SessionRole.Host)
		{
			_eventChannel.ReportOpenedEntity(pos.X, pos.Y);
		}

		BuildingEntityOpenedReceived?.Invoke(pos);
	}

	public void SendBuildingEntityOpened(NetVector2 pos)
	{
		if (!_session.SessionActive)
		{
			return;
		}

		var msg = new BuildingEntityOpenedMsg { Position = pos.ToNetVector2Msg() };
		if (_session.Role == SessionRole.Host)
		{
			_eventChannel.ReportOpenedEntity(pos.X, pos.Y);
			_session.Broadcast(NetMsg.BuildingEntityOpened, msg);
		}
		else
		{
			_sender.Send(_session.HostSteamId, NetMsg.BuildingEntityOpened, msg);
		}
	}

	public void SendBuildingEntityDamaged(NetVector2 pos, float damage, bool playHitSound = true, bool playHitFlash = false)
	{
		if (!_session.SessionActive)
		{
			return;
		}

		var msg = new BuildingEntityDamagedMsg
		{
			Position = pos.ToNetVector2Msg(),
			Damage = damage,
			PlayHitSound = playHitSound,
			PlayHitFlash = playHitFlash,
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

	public void SendBlockPlacedReport(int x, int y, ushort block)
	{
		if (!_session.SessionActive)
		{
			return;
		}
		RecordPendingBlockReport(x, y, block);
		_sender.Send(_session.HostSteamId, NetMsg.BlockPlaced,
			new BlockPlacedMsg { X = x, Y = y, Block = block });
	}

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

	public event Action<RadiationLineStateMsg>? RadiationLineStateReceived;

	public void FireRadiationLineStateReceived(RadiationLineStateMsg state) =>
		RadiationLineStateReceived?.Invoke(state);

	public void SetRadiationLineState(RadiationLineStateMsg state) => RadiationLineState = state;

	public void BroadcastRadiationLineState(RadiationLineStateMsg state)
	{
		RadiationLineState = state;
		if (_session.Role != SessionRole.Host || !_session.SessionActive)
		{
			return;
		}

		_session.Broadcast(NetMsg.RadiationLineState, state);
		_log.LogDebug("Broadcast radiation-line state active={Active}, timeGone={TimeGone:F2}.", state.Active, state.TimeGone);
	}

	public void SendRadiationLineState(ulong targetSteamId)
	{
		if (_session.Role != SessionRole.Host || !_session.SessionActive || RadiationLineState is null)
		{
			return;
		}

		_sender.Send(targetSteamId, NetMsg.RadiationLineState, RadiationLineState);
		_log.LogDebug("Sent radiation-line state (active={Active}, timeGone={TimeGone:F2}) to {Peer}.",
			RadiationLineState.Active, RadiationLineState.TimeGone, targetSteamId);
	}

	public void ReportBlockState(int x, int y, ushort block)
	{
		if (_session.Role != SessionRole.Host || !_session.SessionActive)
		{
			return;
		}

		if (_damagedBlocks.Count >= MaxDamagedBlocks && !_damagedBlocks.ContainsKey((x, y)))
		{
			return;
		}

		_damagedBlocks[(x, y)] = block;
	}

	public void RemoveBlockState(int x, int y)
	{
		if (_session.Role != SessionRole.Host || !_session.SessionActive)
		{
			return;
		}

		_damagedBlocks.Remove((x, y));
	}

	public void ResetDamagedBlocks()
	{
		_damagedBlocks.Clear();
		_blockDamageRegistry.Reset();
		_eventChannel.ResetConsumptions();
		_eventChannel.ResetOpenedEntities();
		_eventChannel.ResetBuildingEntityHealth();
		_eventChannel.ResetTrapLayouts();
	}

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

	public void SendWorldJoin(bool isTutorial)
	{
		if (!_session.SessionActive)
		{
			return;
		}

		var msg = new WorldJoinMsg { IsTutorial = isTutorial };
		foreach (var member in _session.Members)
		{
			if (member.Handshaken && !member.InWorld)
			{
				_sender.Send(member.SteamId, NetMsg.WorldJoin, msg);
			}
		}

		_log.LogInformation("World join sent to {Members} members (tutorial: {Tutorial}).",
			_session.Members.Count(m => m.Handshaken && !m.InWorld), isTutorial);
	}

	public void SendWorldJoinTo(ulong steamId)
	{
		if (_session.Role != SessionRole.Host || !_session.SessionActive)
		{
			return;
		}

		if (!_session.TryGetMember(steamId, out var member) || !member.Handshaken || member.InWorld)
		{
			_log.LogDebug("[Respawn] targeted world join to {Peer} skipped (not a handshaken menu-side member).", steamId);
			return;
		}

		var tutorial = WorldParams?.IsTutorial ?? false;
		_sender.Send(steamId, NetMsg.WorldJoin, new WorldJoinMsg { IsTutorial = tutorial });
		_log.LogInformation("[Respawn] sent targeted world join to {Peer} (tutorial: {Tutorial}).", steamId, tutorial);
	}

	public void PublishWorldParams(WorldStartParams parameters)
	{
		WorldParams = parameters;
		_log.LogInformation("Stored host world params ({StateBytes} bytes); kernel batches carry them to guests.",
			parameters.RandomState.Length);
	}

	public void SendBlockDamaged(NetVector2 worldPos, float damage, bool metalBonus, IReadOnlyList<BlockDropEntryMsg>? drops, IReadOnlyList<TrapDropEntryMsg>? buildingDrops)
	{
		if (!_session.SessionActive)
		{
			return;
		}

		var msg = new BlockDamagedMsg
		{
			Position = worldPos.ToNetVector2Msg(),
			Damage = damage,
			MetalBonus = metalBonus,
			Drops = drops is { Count: > 0 } ? [.. drops] : null,
			BuildingDrops = buildingDrops is { Count: > 0 } ? [.. buildingDrops] : null,
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

	public void BroadcastBlockDamaged(ulong excludeSteamId, NetVector2 worldPos, float damage, bool metalBonus, IReadOnlyList<BlockDropEntryMsg>? drops, IReadOnlyList<TrapDropEntryMsg>? buildingDrops)
	{
		if (_session.Role != SessionRole.Host || !_session.SessionActive)
		{
			return;
		}

		var msg = new BlockDamagedMsg
		{
			Position = worldPos.ToNetVector2Msg(),
			Damage = damage,
			MetalBonus = metalBonus,
			Drops = drops is { Count: > 0 } ? [.. drops] : null,
			BuildingDrops = buildingDrops is { Count: > 0 } ? [.. buildingDrops] : null,
		};
		_session.BroadcastExcept(excludeSteamId, NetMsg.BlockDamaged, msg);
	}

	internal void ResetSessionState()
	{
		WorldParams = null;
		RadiationLineState = null;
		_damagedBlocks.Clear();
		_pendingBlockReports.Clear();
		_pendingReportOverflowLogged = false;
		_blockDamageRegistry.Reset();
		_eventChannel.ResetConsumptions();
		_eventChannel.ResetOpenedEntities();
		_eventChannel.ResetBuildingEntityHealth();
		_eventChannel.ResetTrapLayouts();
	}

	// ---- Guest block-report recovery (audit gap W1) ----

	/// <summary>How many unacknowledged guest block reports are waiting for the host's answer (the fallback pump's work check).</summary>
	internal int PendingBlockReportCount => _pendingBlockReports.Count;

	/// <summary>
	/// Guest only: re-report every unacknowledged block mutation to the host.
	/// Each entry is one <see cref="NetMsg.BlockPlaced"/> report, so the host's
	/// existing first-writer-wins arbitration handles it unchanged (idempotent
	/// — a cell the host already agrees with is answered, not re-applied).
	/// Called by <see cref="BlockReportFallbackPump"/>; a no-op when nothing is
	/// outstanding, when this side is not a guest, or when the session ended.
	/// </summary>
	public void ResendPendingBlockReports()
	{
		if (_session.Role != SessionRole.Guest || !_session.SessionActive || _pendingBlockReports.Count == 0)
		{
			return;
		}

		foreach (var entry in _pendingBlockReports.Entries)
		{
			_sender.Send(_session.HostSteamId, NetMsg.BlockPlaced,
				new BlockPlacedMsg { X = entry.X, Y = entry.Y, Block = entry.Block });
		}

		_log.LogInformation("[BlockSync] re-reported {Count} unacknowledged block mutation(s) to the host.",
			_pendingBlockReports.Count);
	}

	/// <summary>
	/// Host only: answer a guest's block report with the host's authoritative
	/// block at that cell. Sent when the report was refused (first-writer-wins:
	/// the host's cell stands) — the reporter applies the correction and drops
	/// its pending entry. An ACCEPTED report needs no correction: its relay now
	/// includes the reporter (the echo is the acknowledgement).
	/// </summary>
	public void SendBlockPlacedCorrection(ulong targetSteamId, int x, int y, ushort block)
	{
		if (_session.Role != SessionRole.Host || !_session.SessionActive || targetSteamId == 0)
		{
			return;
		}

		_sender.Send(targetSteamId, NetMsg.BlockPlaced, new BlockPlacedMsg { X = x, Y = y, Block = block });
		_log.LogDebug("[BlockSync] answered {Peer}'s report at ({X},{Y}) with the authoritative block {Block}.",
			targetSteamId, x, y, block);
	}

	/// <summary>Guest: the host answered for this cell (relay or correction) — its value is authoritative, the pending report is done.</summary>
	private void OnBlockPlacedReceived(ulong sender, int x, int y, ushort block)
	{
		if (_session.Role == SessionRole.Guest && _pendingBlockReports.Remove(x, y))
		{
			if (_pendingBlockReports.Count == 0)
			{
				_pendingReportOverflowLogged = false; // the overflow episode ended — a later fill must log again
			}

			_log.LogDebug("[BlockSync] host answered ({X},{Y}) — dropped the pending report ({Remaining} left).",
				x, y, _pendingBlockReports.Count);
		}

		BlockPlacedReceived?.Invoke(sender, x, y, block);
	}

	/// <summary>
	/// Record the cell as unacknowledged and send the live report. The record
	/// happens BEFORE the send: a send that never lands is exactly what the
	/// fallback exists for. A newer write at the same cell supersedes the
	/// older report (the host only needs the current value).
	/// </summary>
	private void RecordPendingBlockReport(int x, int y, ushort block)
	{
		if (_pendingBlockReports.Report(x, y, block))
		{
			return;
		}

		if (_pendingReportOverflowLogged)
		{
			return;
		}

		_pendingReportOverflowLogged = true;
		_log.LogWarning("[BlockSync] pending block-report table is full ({Cap} cells) — new cells are not re-reported until the host answers (cell ({X},{Y}) dropped).",
			_pendingBlockReports.Cap, x, y);
	}

	/// <summary>
	/// Guest: a new world/layer baseline was applied — every pending report
	/// belongs to the previous world and must not be arbitrated into the new
	/// one. Called from the guest's world-params apply boundary (the adapter's
	/// WorldParamsService), symmetric to the host's ResetDamagedBlocks at the
	/// generation boundary. The world-entry completion marker deliberately does
	/// NOT clear the table: a reconnect-while-in-world keeps the guest's local
	/// mutations, and re-reporting them is exactly the recovery.
	/// </summary>
	public void ResetPendingBlockReports()
	{
		if (_pendingBlockReports.Count > 0)
		{
			_log.LogInformation("[BlockSync] world baseline replaced — cleared {Count} pending block report(s) from the previous world.",
				_pendingBlockReports.Count);
			_pendingBlockReports.Clear();
		}

		_pendingReportOverflowLogged = false;
	}
}
