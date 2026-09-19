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
	KernelWorldGenerationSource generations)
{
	private readonly ISessionControl _session = session;
	private readonly PacketSender _sender = sender;
	private readonly ILogger<WorldService> _log = log;
	private readonly EntityEventChannel _eventChannel = eventChannel;
	private readonly KernelWorldGenerationSource _generations = generations;

	/// <summary>
	/// Host-side block-difference table: block-space position → current block id,
	/// for every block whose state deviates from the generated baseline. Mined,
	/// destroyed, built and reverted blocks all land here.
	/// </summary>
	private readonly Dictionary<(int, int), DamagedBlock> _damagedBlocks = [];

	/// <summary>Table cap — a fully-mined world would otherwise grow without bound. Internal so the pending-report table's own cap can be asserted against it.</summary>
	internal const int MaxDamagedBlocks = 65536;

	public WorldStartParams? WorldParams { get; set; }

	/// <summary>
	/// Host: the run/layer clocks the adapter last read off the live world. Null until a
	/// world has been captured — the message is never fabricated from zeros (a zero
	/// clock is a real value a receiver would write onto its own statics).
	/// </summary>
	public RunClockFacts? RunFacts { get; set; }

	public RadiationLineStateMsg? RadiationLineState { get; private set; }

	// ---- World message flow ----

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

	public event Action<RunFactsMsg, bool>? RunFactsReceived;

	/// <summary>Guest: the host's clocks arrived. <paramref name="layerTimerApplies"/> is false when the message's generation is not this side's — the clock is still applied (it is run-scoped), the layer timer is not.</summary>
	public void FireRunFactsReceived(RunFactsMsg facts, bool layerTimerApplies) => RunFactsReceived?.Invoke(facts, layerTimerApplies);

	/// <summary>
	/// Host only: send the run/layer clocks this host read off its live world, stamped
	/// with the kernel run baseline's generation — the same identity every layer-relative
	/// world report carries, so the receiver can refuse a value captured in another layer.
	/// Nothing is sent when no run baseline or no captured value exists: the receiver keeps
	/// its own values and names the absence, which is the pre-message behaviour rather than
	/// a guessed clock.
	/// </summary>
	public void SendRunFacts(ulong targetSteamId)
	{
		if (_session.Role != SessionRole.Host || targetSteamId == 0)
		{
			return;
		}

		var facts = RunFacts;
		if (facts is not { } captured || captured.Failure is not null)
		{
			_log.LogDebug("[RunFacts] nothing to send to {Peer} — this host has no captured run clock; it keeps its own clock and layer timer.", targetSteamId);
			return;
		}

		if (_generations.Current is not { } generation)
		{
			// No committed run baseline means no identity to stamp the values with, and an
			// unstamped absolute clock is exactly the value that could be written onto the
			// wrong layer.
			_log.LogDebug("[RunFacts] no committed run baseline — the run clock is not sent to {Peer}.", targetSteamId);
			return;
		}

		_sender.Send(targetSteamId, NetMsg.RunFacts, new RunFactsMsg
		{
			RunEpoch = generation.RunEpoch,
			LayerIndex = generation.LayerIndex,
			RunClockBase = captured.RunClockBase,
			LayerTimeSpent = captured.LayerTimeSpent,
			MaxTimePerLayer = captured.MaxTimePerLayer,
		});
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

	public void SendWorldJoin(bool isTutorial, ulong runEpoch)
	{
		if (!_session.SessionActive)
		{
			return;
		}

		// The run identity rides the instruction: it is the edge at which a NEW run
		// legitimately begins, and the member validates every checkpoint set it is
		// about to receive against it (the entry group follows this instruction).
		var msg = new WorldJoinMsg { IsTutorial = isTutorial, RunEpoch = runEpoch };
		foreach (var member in _session.Members)
		{
			if (member.Handshaken && !member.InWorld)
			{
				_sender.Send(member.SteamId, NetMsg.WorldJoin, msg);
			}
		}

		_log.LogInformation("World join sent to {Members} members (tutorial: {Tutorial}, run {Epoch}).",
			_session.Members.Count(m => m.Handshaken && !m.InWorld), isTutorial, runEpoch);
	}

	public void SendWorldJoinTo(ulong steamId, ulong runEpoch)
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
		_sender.Send(steamId, NetMsg.WorldJoin, new WorldJoinMsg { IsTutorial = tutorial, RunEpoch = runEpoch });
		_log.LogInformation("[Respawn] sent targeted world join to {Peer} (tutorial: {Tutorial}, run {Epoch}).", steamId, tutorial, runEpoch);
	}

	public void PublishWorldParams(WorldStartParams parameters)
	{
		WorldParams = parameters;
		_log.LogInformation("Stored host world params ({StateBytes} bytes); kernel batches carry them to guests.",
			parameters.RandomState.Length);
	}

	internal void ResetSessionState()
	{
		WorldParams = null;
		// The captured run clocks belong to the world that read them: a session that
		// ended must never hand the next run's members a dead run's clock.
		RunFacts = null;
		RadiationLineState = null;
		_damagedBlocks.Clear();
		_eventChannel.ResetConsumptions();
		_eventChannel.ResetOpenedEntities();
		_eventChannel.ResetBuildingEntityHealth();
		_eventChannel.ResetTrapLayouts();
	}

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
