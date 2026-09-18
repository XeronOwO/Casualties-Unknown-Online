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
/// backfill. The guest's pending-block-report RECOVERY STATE lives in
/// <see cref="GuestBlockReportBookkeeping"/> (this type only sends and answers
/// reports), and the start-gate lifecycle stays in <see cref="WorldService"/>.
/// </summary>
internal sealed class WorldStateMessageService(
	ISessionControl session,
	PacketSender sender,
	ILogger<WorldService> log,
	EntityEventChannel eventChannel,
	INativeWorldFacts? nativeWorldFacts)
{
	private readonly ISessionControl _session = session;
	private readonly PacketSender _sender = sender;
	private readonly ILogger<WorldService> _log = log;
	private readonly EntityEventChannel _eventChannel = eventChannel;

	/// <summary>
	/// The guest-side report recovery (audit gaps W1/W2): the two pending tables
	/// (unacknowledged block state, unacknowledged partial damage), their
	/// re-sends and their answers. Its one native-port use is the merge half of a
	/// guest's absolute damage report; a Runtime-only composition has no port,
	/// reads and merges nothing, and says so instead of inventing rows.
	/// </summary>
	private readonly GuestReportRecovery _guestReports = new(session, sender, nativeWorldFacts, log);

	/// <summary>The partial-damage snapshot's send half — the other world flow that reads the GAME's own tables.</summary>
	private readonly BlockDamageSnapshotSender _blockDamageSnapshot = new(session, sender, nativeWorldFacts, log);

	/// <summary>
	/// Host-side block-difference table: block-space position → current block id,
	/// for every block whose state deviates from the generated baseline. Mined,
	/// destroyed, built and reverted blocks all land here.
	/// </summary>
	private readonly Dictionary<(int, int), DamagedBlock> _damagedBlocks = [];

	/// <summary>Table cap — a fully-mined world would otherwise grow without bound. Internal so the pending-report table's own cap can be asserted against it.</summary>
	internal const int MaxDamagedBlocks = 65536;

	public WorldStartParams? WorldParams { get; set; }

	public RadiationLineStateMsg? RadiationLineState { get; private set; }

	// ---- Block damage (late-joiner backfill) ----

	public event Action<IReadOnlyList<BlockDamageEntryMsg>>? BlockDamageSnapshotReceived;

	/// <summary>
	/// Guest: the host's partial-damage snapshot arrived — it is also the ANSWER
	/// to every outstanding absolute re-report whose cell it names (the
	/// world-entry / 60 s snapshot and the per-report answer share this message),
	/// so those pending entries are done.
	/// </summary>
	public void FireBlockDamageSnapshotReceived(IReadOnlyList<BlockDamageEntryMsg> entries)
	{
		if (_session.Role == SessionRole.Guest)
		{
			_guestReports.AnswerDamage(entries);
		}

		BlockDamageSnapshotReceived?.Invoke(entries);
	}

	/// <summary>Host only: send the partial damage the GAME's own list holds (see <see cref="BlockDamageSnapshotSender"/>).</summary>
	public void SendBlockDamageSnapshot(ulong targetSteamId) => _blockDamageSnapshot.Send(targetSteamId);

	// ---- World message flow ----

	public event Action<ulong, NetVector2, float, bool, IReadOnlyList<BlockDropEntryMsg>?, IReadOnlyList<TrapDropEntryMsg>?>? BlockDamagedReceived;

	public void FireBlockDamagedReceived(ulong sender, NetVector2 pos, float damage, bool metalBonus, IReadOnlyList<BlockDropEntryMsg>? drops, IReadOnlyList<TrapDropEntryMsg>? buildingDrops) =>
		// The break-drop report's acknowledgement is NOT cleared here: the cell a
		// report belongs to is the game's own world→cell conversion (it offsets by
		// the world's half extent), which only the adapter can run — BlockBreakSync
		// answers the pending drop report with the cell it resolved.
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

	/// <summary>Guest: a block was placed locally — record it as an unacknowledged pending report and send it (the host arbitrates + answers; a swallowed report is re-reported by the fallback).</summary>
	public void SendBlockPlacedReport(int x, int y, ushort block)
	{
		if (!_session.SessionActive)
		{
			return;
		}

		_guestReports.ReportBlock(x, y, block);
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

		// A live write: whatever support loss this air transition causes is settled
		// HERE (on the side that owns the world), so a receiver re-runs it.
		_damagedBlocks[(x, y)] = new DamagedBlock(x, y, block, SupportLossSettled: false);
	}

	public void RemoveBlockState(int x, int y)
	{
		if (_session.Role != SessionRole.Host || !_session.SessionActive)
		{
			return;
		}

		_damagedBlocks.Remove((x, y));
	}

	public void SendBlockStateSnapshot(ulong targetSteamId)
	{
		if (_session.Role != SessionRole.Host || _damagedBlocks.Count == 0)
		{
			return;
		}

		var msg = new BlockStateMsg
		{
			Blocks = [.. _damagedBlocks.Values.Select(cell => new BlockStateEntryMsg
			{
				X = cell.X,
				Y = cell.Y,
				Block = cell.Block,
				SupportLossSettled = cell.SupportLossSettled,
			})],
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
		_guestReports.ResetBlocks();
		_guestReports.ResetDamages();
		_guestReports.ResetBreakDrops();
		_eventChannel.ResetConsumptions();
		_eventChannel.ResetOpenedEntities();
		_eventChannel.ResetBuildingEntityHealth();
		_eventChannel.ResetTrapLayouts();
	}

	// ---- Guest report recovery (audit gaps W1/W2) ----
	// The re-send/answer WIRE logic lives in GuestReportRecovery; the recovery
	// STATE lives in its two bookkeeping collaborators. These members are only
	// the surface WorldService and the handlers call.

	/// <summary>How many unacknowledged guest block reports are waiting for the host's answer (the fallback pump's work check).</summary>
	internal int PendingBlockReportCount => _guestReports.PendingBlockCount;

	/// <summary>Guest only: re-report every unacknowledged block mutation to the host (the fallback pump's action).</summary>
	public void ResendPendingBlockReports() => _guestReports.ResendBlocks();

	/// <summary>Host only: answer a guest's refused block report with the host's authoritative block at that cell.</summary>
	public void SendBlockPlacedCorrection(ulong targetSteamId, int x, int y, ushort block) =>
		_guestReports.SendBlockPlacedCorrection(targetSteamId, x, y, block);

	/// <summary>Guest: the host answered for this cell (relay or correction) — its value is authoritative, the pending report is done.</summary>
	private void OnBlockPlacedReceived(ulong sender, int x, int y, ushort block)
	{
		if (_session.Role == SessionRole.Guest)
		{
			_guestReports.AnswerBlock(x, y);
		}

		BlockPlacedReceived?.Invoke(sender, x, y, block);
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
	public void ResetPendingBlockReports() => _guestReports.ResetBlocks();

	// ---- Guest partial-damage report recovery (audit gap W2) ----

	/// <summary>How many unacknowledged guest partial-damage reports are waiting for the host's answer (the fallback pump's work check).</summary>
	internal int PendingBlockDamageReportCount => _guestReports.PendingDamageCount;

	/// <summary>The guest report recovery (W1 block state, W2 partial damage, W1's drop half) — <see cref="WorldService"/> relays that surface to the adapter without this message surface growing a member per channel (the 600-line gate).</summary>
	internal GuestReportRecovery GuestReports => _guestReports;

	/// <summary>Guest only: record the cell's current ABSOLUTE damage before the live delta report goes out (the fallback's re-report source).</summary>
	public void ReportBlockDamage(int x, int y, float damage) => _guestReports.ReportDamage(x, y, damage);

	/// <summary>Either role: the cell went air — its pending partial-damage report dies with the block.</summary>
	public void ForgetPendingBlockDamage(int x, int y) => _guestReports.ForgetDamage(x, y);

	/// <summary>Guest only: a new world/layer baseline was applied — the previous world's pending partial-damage reports are dropped.</summary>
	public void ResetPendingBlockDamageReports() => _guestReports.ResetDamages();

	/// <summary>Guest only: re-report every unacknowledged cell's absolute damage to the host (the fallback pump's action).</summary>
	public void ResendPendingBlockDamageReports() => _guestReports.ResendDamages();

	/// <summary>Host only: merge a guest's absolute partial-damage report and answer every reported cell authoritatively.</summary>
	public void HandleBlockDamageReport(ulong sender, IReadOnlyList<BlockDamageEntryMsg> entries) =>
		_guestReports.HandleDamageReport(sender, entries);

	// ---- World-fact capture and restore (the save system's world diff) ----
	// The save side reads these tables as the WIRE shapes a late joiner receives
	// (WorldFactLifecycle); the tables stay owned here, so the accessors below are
	// the only surface the save layer gets — no read-only view of the live
	// dictionary escapes this type.

	/// <summary>This peer's session role — the save layer's authority predicate reads it (a guest never writes a world archive).</summary>
	internal SessionRole Role => _session.Role;

	/// <summary>The host's block difference table in the wire shape the late-joiner snapshot already sends.</summary>
	internal IReadOnlyList<BlockStateEntryMsg> CaptureBlockStates() =>
	[.. _damagedBlocks.Values.Select(cell => new BlockStateEntryMsg
	{
		X = cell.X,
		Y = cell.Y,
		Block = cell.Block,
		SupportLossSettled = cell.SupportLossSettled,
	})];

	/// <summary>
	/// Host: upsert one restored block state. The cell is the identity, so a
	/// repeated cell simply wins (a snapshot never carries the same cell twice).
	/// Returns false when the table's cap refused a cell it does not already
	/// hold — the restore report counts it instead of reporting a clean restore.
	/// </summary>
	internal bool ApplyBlockState(BlockStateEntryMsg entry)
	{
		if (_damagedBlocks.Count >= MaxDamagedBlocks && !_damagedBlocks.ContainsKey((entry.X, entry.Y)))
		{
			_log.LogWarning("[SaveFacts] block-state ({X},{Y}) skipped: the {Cap}-cell table is full.", entry.X, entry.Y, MaxDamagedBlocks);
			return false;
		}

		_damagedBlocks[(entry.X, entry.Y)] = new DamagedBlock(entry.X, entry.Y, entry.Block, entry.SupportLossSettled);
		return true;
	}

	/// <summary>
	/// A new world/layer is generating: every per-layer RUNTIME world-fact table
	/// starts empty — the block diff, and the kernel-backed world-entity tables the
	/// layer boundary always cleared. The partial block damage is not among them:
	/// it has no Runtime table, and the game regenerates its own list with the
	/// layer. The radiation line is deliberately kept: it is run state the boundary
	/// never touched.
	/// </summary>
	internal void ResetPerLayer() => ClearWorldFacts(includeRadiationLine: false, includeKernelWorldEntities: true);

	/// <summary>
	/// The save layer's restore reset: the snapshot is about to put back the whole
	/// Runtime world-fact set, so the block diff AND the radiation line start empty.
	///
	/// It deliberately does NOT clear the kernel-backed world-entity tables
	/// (consumptions, opened entities, building health): those are written THROUGH
	/// to the kernel by their registries, and the restore path runs
	/// <c>_kernel.Restore</c> — clearing them here would erase the facts that
	/// restore just applied. They are not part of the save layer's fact set; the
	/// kernel checkpoint owns them.
	/// </summary>
	internal void ResetForRestore() => ClearWorldFacts(includeRadiationLine: true, includeKernelWorldEntities: false);

	private void ClearWorldFacts(bool includeRadiationLine, bool includeKernelWorldEntities)
	{
		_damagedBlocks.Clear();
		if (includeRadiationLine)
		{
			RadiationLineState = null;
		}

		if (includeKernelWorldEntities)
		{
			_eventChannel.ResetConsumptions();
			_eventChannel.ResetOpenedEntities();
			_eventChannel.ResetBuildingEntityHealth();
			_eventChannel.ResetTrapLayouts();
		}
	}
}
