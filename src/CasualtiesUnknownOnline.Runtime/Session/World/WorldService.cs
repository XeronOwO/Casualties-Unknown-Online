using System;
using System.Collections.Generic;
using CasualtiesUnknownOnline.GameState;
using CasualtiesUnknownOnline.GameState.Domains.Fluids;
using CasualtiesUnknownOnline.GameState.Domains.World;
using CasualtiesUnknownOnline.Runtime.Protocol;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using CasualtiesUnknownOnline.Runtime.Session.Items;
using CasualtiesUnknownOnline.Runtime.Session.ProjectionHealth;
using CasualtiesUnknownOnline.Runtime.Time;
using Microsoft.Extensions.Logging;

namespace CasualtiesUnknownOnline.Runtime.Session.World;

/// <summary>
/// The world domain coordinator. It owns the host start-gate lifecycle and the
/// world-start parameters pointer, while the block/building/world-state message
/// flow lives in <see cref="WorldStateMessageService"/> and the entity/trader/
/// speech/chat channel forwarding lives in <see cref="WorldChannelRelay"/>.
/// This facade keeps <see cref="IWorldControl"/> stable for packet handlers and
/// the Game Adapter without turning one class into a mixed god-object.
/// </summary>
public sealed class WorldService : IWorldControl, IDisposable
{
	private readonly ISessionControl _session;
	private readonly ILogger<WorldService> _log;
	private readonly WorldChannelRelay _channels;
	private readonly WorldStateMessageService _messages;
	private readonly PendingReportFallback _blockReportFallback;
	private readonly ItemKernelAuthority _kernelAuthority;
	private readonly FluidKernelProjection _fluidKernel;
	private readonly FluidKernelReadProjection _fluidKernelRead;
	private readonly ProjectionHealthCoordinator _projectionHealth;

	/// <summary>
	/// Host only: a run is in progress but the host has not entered the world
	/// yet (click moment → world entry). A handshake during this window may
	/// follow immediately.
	/// </summary>
	public bool HostRunPending => _startGate.HostRunPending;

	/// <summary>Host only: the start-gate lifecycle (armed member set, 30 s fallback) — its own responsibility.</summary>
	private readonly WorldStartGate _startGate;

	public WorldStartParams? WorldParams
	{
		get => _messages.WorldParams;
		set => _messages.WorldParams = value;
	}

	public RadiationLineStateMsg? RadiationLineState => _messages.RadiationLineState;

	/// <summary>Guest: the host released the start gate — start playing (or, for a late joiner, enter directly).</summary>
	public event Action? WorldReadyReceived;

	public WorldService(
		ISessionControl session,
		PacketSender sender,
		ITimeSource time,
		ILogger<WorldService> log,
		EntityEventChannel eventChannel,
		RuntimeEntityChannel runtimeEntityChannel,
		TradeChannel tradeChannel,
		SpeechChannel speechChannel,
		ChatChannel chatChannel,
		LocationPingChannel locationPingChannel,
		BlockDamageRegistry blockDamageRegistry,
		ItemKernelAuthority kernelAuthority,
		FluidKernelProjection fluidKernel,
		FluidKernelReadProjection fluidKernelRead,
		ProjectionHealthCoordinator projectionHealth)
	{
		_session = session;
		_log = log;
		_channels = new WorldChannelRelay(eventChannel, runtimeEntityChannel, tradeChannel, speechChannel, chatChannel, locationPingChannel);
		_messages = new WorldStateMessageService(session, sender, log, eventChannel, blockDamageRegistry);
		_blockReportFallback = new PendingReportFallback(session);
		_startGate = new WorldStartGate(session, sender, time, log);
		_kernelAuthority = kernelAuthority;
		_fluidKernel = fluidKernel;
		_fluidKernelRead = fluidKernelRead;
		_projectionHealth = projectionHealth;
		_projectionHealth.Register(new ProjectionDomain("run", RebuildRunFromKernel, () => _kernelAuthority.CurrentGlobalRevision));

		_kernelAuthority.BatchApplied += OnRunBatchApplied;
		_kernelAuthority.BatchCommitted += OnRunBatchCommitted;
		_kernelAuthority.CheckpointRestored += OnRunCheckpointRestored;
		session.SessionEnded += OnSessionEnded;
	}

	public void SetHostRunPending(bool pending) => _startGate.SetHostRunPending(pending);

	public void FireWorldReadyReceived() => WorldReadyReceived?.Invoke();

	// ---- Start gate lifecycle (WorldStartGate owns the state) ----

	public bool StartStartGate() => _startGate.Arm();

	public void NotifyMemberInWorld(ulong steamId) => _startGate.NotifyMemberInWorld(steamId);

	public void MaybeForceStartGate() => _startGate.PumpTimeout();

	public bool StartGateActive => _startGate.Active;

	public int StartGateRemainingMs => _startGate.RemainingMs;

	// ---- Guest block-report fallback (audit gap W1) ----

	/// <summary>Test/observability seam (InternalsVisibleTo): how many unacknowledged guest block reports are outstanding.</summary>
	internal int PendingBlockReportCount => _messages.PendingBlockReportCount;

	/// <summary>The guest block-report fallback's time edge (driven by <see cref="WorldReportFallbackPump"/>; the cadence policy lives in <see cref="PendingReportFallback"/>).</summary>
	internal void PumpBlockReportFallback(long nowMs) =>
		_blockReportFallback.Pump(nowMs, _messages.PendingBlockReportCount, _messages.ResendPendingBlockReports);

	// ---- Session reset ----

	private void ResetSessionState()
	{
		_startGate.Reset();
		_blockReportFallback.Reset();
		_channels.ResetRuntimeEntities();
		_channels.ResetPendingEntityReports();
		WorldParams = null;
		_messages.ResetSessionState();
	}

	private void OnSessionEnded() => ResetSessionState();

	public void Dispose()
	{
		_kernelAuthority.BatchApplied -= OnRunBatchApplied;
		_kernelAuthority.BatchCommitted -= OnRunBatchCommitted;
		_kernelAuthority.CheckpointRestored -= OnRunCheckpointRestored;
		_session.SessionEnded -= OnSessionEnded;
	}

	// ---- Channel relay ----

	public event Action<ulong, EntityEventMsg>? EntityEventReceived { add => _channels.EntityEventReceived += value; remove => _channels.EntityEventReceived -= value; }

	public void FireEntityEventReceived(ulong sender, EntityEventMsg msg) => _channels.FireEntityEventReceived(sender, msg);

	public void SendEntityEvent(EntityEventMsg msg, float? buildingHealth = null) => _channels.SendEntityEvent(msg, buildingHealth);

	public void BroadcastEntityEvent(ulong excludeSteamId, EntityEventMsg msg) => _channels.BroadcastEntityEvent(excludeSteamId, msg);

	public void SendDynamiteExplosion(ulong itemInstanceId, NetVector2 position) => _channels.SendDynamiteExplosion(itemInstanceId, position);

	public void BroadcastDynamiteExplosion(ulong excludeSteamId, ulong itemInstanceId, NetVector2 position) => _channels.BroadcastDynamiteExplosion(excludeSteamId, itemInstanceId, position);

	public event Action<ulong, ulong, NetVector2>? DynamiteExplosionReceived { add => _channels.DynamiteExplosionReceived += value; remove => _channels.DynamiteExplosionReceived -= value; }

	public void FireDynamiteExplosionReceived(ulong sender, ulong itemInstanceId, NetVector2 position) => _channels.FireDynamiteExplosionReceived(sender, itemInstanceId, position);

	public void SendWorldBloodSpawn(WorldBloodSpawnMsg msg) => _channels.SendWorldBloodSpawn(msg);

	public void BroadcastWorldBloodSpawn(ulong excludeSteamId, WorldBloodSpawnMsg msg) => _channels.BroadcastWorldBloodSpawn(excludeSteamId, msg);

	public event Action<ulong, WorldBloodSpawnMsg>? WorldBloodSpawnReceived { add => _channels.WorldBloodSpawnReceived += value; remove => _channels.WorldBloodSpawnReceived -= value; }

	public void FireWorldBloodSpawnReceived(ulong sender, WorldBloodSpawnMsg msg) => _channels.FireWorldBloodSpawnReceived(sender, msg);

	public void ReportTrapConsumed(EntityEventKind kind, float x, float y, byte extra) => _channels.ReportTrapConsumed(kind, x, y, extra);

	public void ReportTrapState(EntityEventKind kind, float x, float y, byte extra) => _channels.ReportTrapState(kind, x, y, extra);

	public void ReportTrapEvent(EntityEventKind kind, float x, float y, byte extra, float? buildingHealth = null, IReadOnlyList<BuildingEntityHealthEntryMsg>? additionalHealth = null, IReadOnlyList<TrapDropEntryMsg>? drops = null, ulong? dropActor = null) => _channels.ReportTrapEvent(kind, x, y, extra, buildingHealth, additionalHealth, drops, dropActor);

	public event Action<ulong, EntitySpawnedMsg>? EntitySpawnedReceived { add => _channels.EntitySpawnedReceived += value; remove => _channels.EntitySpawnedReceived -= value; }

	public void FireEntitySpawnedReceived(ulong sender, EntitySpawnedMsg msg) => _channels.FireEntitySpawnedReceived(sender, msg);

	public void SendEntitySpawned(EntitySpawnedMsg msg) => _channels.SendEntitySpawned(msg);

	public void BroadcastEntitySpawned(ulong excludeSteamId, EntitySpawnedMsg msg) => _channels.BroadcastEntitySpawned(excludeSteamId, msg);

	/// <summary>Host only: send the accepted runtime-entity creation table to one member (world entry, or the 60 s cycle).</summary>
	public void SendRuntimeEntitySnapshot(ulong targetSteamId) => _channels.SendRuntimeEntitySnapshot(targetSteamId);

	/// <summary>Guest: the host's absolute runtime-entity table arrived — apply every entry (create missing, dedupe existing) and acknowledge the matching pending reports.</summary>
	public void FireRuntimeEntitySnapshotReceived(ulong sender, IReadOnlyList<EntitySpawnedMsg> entries) =>
		_channels.FireRuntimeEntitySnapshotReceived(sender, entries);

	/// <summary>Either side: a runtime-created entity's local copy died — drop its accepted record (host) or its pending re-report (guest).</summary>
	public void ReportRuntimeEntityDestroyed(string id, float x, float y) => _channels.ReportRuntimeEntityDestroyed(id, x, y);

	/// <summary>Guest: a new world/layer baseline was applied — drop every unacknowledged creation report from the previous world.</summary>
	public void ResetPendingEntityReports() => _channels.ResetPendingEntityReports();

	/// <summary>Host only: a new world layer is generating — the accepted runtime-entity creation table starts empty again.</summary>
	public void ResetRuntimeEntities() => _channels.ResetRuntimeEntities();

	public void ReportOpenedEntity(float x, float y) => _channels.ReportOpenedEntity(x, y);

	public void ReportBuildingEntityHealth(float x, float y, float health) => _channels.ReportBuildingEntityHealth(x, y, health);

	public void ReportTrapLayout(EntityEventKind kind, float x, float y, string prefabName) => _channels.ReportTrapLayout(kind, x, y, prefabName);

	public void SendTrapLayoutSnapshot(ulong targetSteamId) => _channels.SendTrapLayoutSnapshot(targetSteamId);

	public event Action<IReadOnlyList<TrapLayoutEntryMsg>>? TrapLayoutReceived { add => _channels.TrapLayoutReceived += value; remove => _channels.TrapLayoutReceived -= value; }

	public void FireTrapLayoutReceived(IReadOnlyList<TrapLayoutEntryMsg> entries) => _channels.FireTrapLayoutReceived(entries);

	/// <summary>Guest-side rebuilt fluid-region facts from the kernel projection (host: empty).</summary>
	public IReadOnlyList<FluidRegionState> FluidRegionFacts => _fluidKernelRead.Regions;

	/// <summary>Guest-side rebuilt fluid-region projection event (raised after checkpoint/batch rebuilds).</summary>
	public event Action<IReadOnlyList<FluidRegionState>>? FluidRegionsProjected
	{
		add => _fluidKernelRead.RegionsProjected += value;
		remove => _fluidKernelRead.RegionsProjected -= value;
	}

	public void ReportFluidRegions(IReadOnlyList<FluidRegionSummary> regions) => _fluidKernel.Sync(regions);

	public void SendFluidRegion(ulong targetSteamId, FluidRegionMsg msg) => _channels.SendFluidRegion(targetSteamId, msg);

	public event Action<FluidRegionMsg>? FluidRegionReceived { add => _channels.FluidRegionReceived += value; remove => _channels.FluidRegionReceived -= value; }

	public void FireFluidRegionReceived(FluidRegionMsg msg) => _channels.FireFluidRegionReceived(msg);

	public void SendFluidInteraction(FluidInteractionMsg msg) => _channels.SendFluidInteraction(msg);

	public void BroadcastFluidInteraction(ulong excludeSteamId, FluidInteractionMsg msg) => _channels.BroadcastFluidInteraction(excludeSteamId, msg);

	public event Action<ulong, FluidInteractionMsg>? FluidInteractionReceived { add => _channels.FluidInteractionReceived += value; remove => _channels.FluidInteractionReceived -= value; }

	public void FireFluidInteractionReceived(ulong sender, FluidInteractionMsg msg) => _channels.FireFluidInteractionReceived(sender, msg);

	public void SendFluidPresentation(ulong targetSteamId, FluidPresentationMsg msg) => _channels.SendFluidPresentation(targetSteamId, msg);

	public event Action<FluidPresentationMsg>? FluidPresentationReceived { add => _channels.FluidPresentationReceived += value; remove => _channels.FluidPresentationReceived -= value; }

	public void FireFluidPresentationReceived(FluidPresentationMsg msg) => _channels.FireFluidPresentationReceived(msg);

	public void SendTraderState(ulong targetSteamId, TraderStateMsg msg) => _channels.SendTraderState(targetSteamId, msg);

	public void BroadcastTraderState(TraderStateMsg msg) => _channels.BroadcastTraderState(msg);

	public event Action<TraderStateMsg>? TraderStateReceived { add => _channels.TraderStateReceived += value; remove => _channels.TraderStateReceived -= value; }

	public void FireTraderStateReceived(TraderStateMsg msg) => _channels.FireTraderStateReceived(msg);

	public void SendTraderAction(TraderActionMsg msg) => _channels.SendTraderAction(msg);

	public event Action<ulong, TraderActionMsg>? TraderActionReceived { add => _channels.TraderActionReceived += value; remove => _channels.TraderActionReceived -= value; }

	public void FireTraderActionReceived(ulong sender, TraderActionMsg msg) => _channels.FireTraderActionReceived(sender, msg);

	public void SendTraderRecruitRequest(TraderRecruitRequestMsg msg) => _channels.SendTraderRecruitRequest(msg);

	public event Action<ulong, TraderRecruitRequestMsg>? TraderRecruitRequestReceived { add => _channels.TraderRecruitRequestReceived += value; remove => _channels.TraderRecruitRequestReceived -= value; }

	public void FireTraderRecruitRequestReceived(ulong sender, TraderRecruitRequestMsg msg) => _channels.FireTraderRecruitRequestReceived(sender, msg);

	public void SendTraderRecruitResult(ulong targetSteamId, TraderRecruitResultMsg msg) => _channels.SendTraderRecruitResult(targetSteamId, msg);

	public event Action<TraderRecruitResultMsg>? TraderRecruitResultReceived { add => _channels.TraderRecruitResultReceived += value; remove => _channels.TraderRecruitResultReceived -= value; }

	public void FireTraderRecruitResultReceived(TraderRecruitResultMsg msg) => _channels.FireTraderRecruitResultReceived(msg);

	public void SendTraderSwing(TraderSwingMsg msg) => _channels.SendTraderSwing(msg);

	public event Action<ulong, TraderSwingMsg>? TraderSwingReceived { add => _channels.TraderSwingReceived += value; remove => _channels.TraderSwingReceived -= value; }

	public void FireTraderSwingReceived(ulong sender, TraderSwingMsg msg) => _channels.FireTraderSwingReceived(sender, msg);

	public void SendSpeech(SpeechMsg msg) => _channels.SendSpeech(msg);

	public void BroadcastSpeech(ulong excludeSteamId, SpeechMsg msg) => _channels.BroadcastSpeech(excludeSteamId, msg);

	public event Action<ulong, SpeechMsg>? SpeechReceived { add => _channels.SpeechReceived += value; remove => _channels.SpeechReceived -= value; }

	public void FireSpeechReceived(ulong sender, SpeechMsg msg) => _channels.FireSpeechReceived(sender, msg);

	public void SendChat(ChatMsg msg) => _channels.SendChat(msg);

	public void BroadcastChat(ulong excludeSteamId, ChatMsg msg) => _channels.BroadcastChat(excludeSteamId, msg);

	public event Action<ulong, ChatMsg>? ChatReceived { add => _channels.ChatReceived += value; remove => _channels.ChatReceived -= value; }

	public void FireChatReceived(ulong sender, ChatMsg msg) => _channels.FireChatReceived(sender, msg);

	public void SendLocationPing(LocationPingMsg msg) => _channels.SendLocationPing(msg);

	public void BroadcastLocationPing(ulong excludeSteamId, LocationPingMsg msg) => _channels.BroadcastLocationPing(excludeSteamId, msg);

	public event Action<ulong, LocationPingMsg>? LocationPingReceived { add => _channels.LocationPingReceived += value; remove => _channels.LocationPingReceived -= value; }

	public void FireLocationPingReceived(ulong sender, LocationPingMsg msg) => _channels.FireLocationPingReceived(sender, msg);

	// ---- World message flow ----

	public event Action<ulong, NetVector2, float, bool, IReadOnlyList<BlockDropEntryMsg>?, IReadOnlyList<TrapDropEntryMsg>?>? BlockDamagedReceived { add => _messages.BlockDamagedReceived += value; remove => _messages.BlockDamagedReceived -= value; }

	public void FireBlockDamagedReceived(ulong sender, NetVector2 pos, float damage, bool metalBonus, IReadOnlyList<BlockDropEntryMsg>? drops, IReadOnlyList<TrapDropEntryMsg>? buildingDrops) => _messages.FireBlockDamagedReceived(sender, pos, damage, metalBonus, drops, buildingDrops);

	public event Action<bool>? WorldJoinReceived { add => _messages.WorldJoinReceived += value; remove => _messages.WorldJoinReceived -= value; }

	public void FireWorldJoinReceived(bool isTutorial) => _messages.FireWorldJoinReceived(isTutorial);

	public event Action? WorldSnapshotCompleteReceived { add => _messages.WorldSnapshotCompleteReceived += value; remove => _messages.WorldSnapshotCompleteReceived -= value; }

	public void FireWorldSnapshotCompleteReceived() => _messages.FireWorldSnapshotCompleteReceived();

	public void SendWorldSnapshotComplete(ulong targetSteamId) => _messages.SendWorldSnapshotComplete(targetSteamId);

	public event Action<IReadOnlyList<DamagedBlock>>? BlockStateReceived { add => _messages.BlockStateReceived += value; remove => _messages.BlockStateReceived -= value; }

	public void FireBlockStateReceived(IReadOnlyList<DamagedBlock> blocks) => _messages.FireBlockStateReceived(blocks);

	public event Action<float, float>? EarthquakeStartReceived { add => _messages.EarthquakeStartReceived += value; remove => _messages.EarthquakeStartReceived -= value; }

	public void FireEarthquakeStartReceived(float duration, float nextDelay) => _messages.FireEarthquakeStartReceived(duration, nextDelay);

	public void BroadcastEarthquakeStart(float duration, float nextDelay) => _messages.BroadcastEarthquakeStart(duration, nextDelay);

	public event Action<IReadOnlyList<KeypadEntryMsg>>? KeypadCodeReceived { add => _messages.KeypadCodeReceived += value; remove => _messages.KeypadCodeReceived -= value; }

	public void FireKeypadCodeReceived(IReadOnlyList<KeypadEntryMsg> codes) => _messages.FireKeypadCodeReceived(codes);

	public void SendKeypadCodes(IReadOnlyList<KeypadEntryMsg> codes) => _messages.SendKeypadCodes(codes);

	public event Action<IReadOnlyList<GeyserStateEntryMsg>>? GeyserStateReceived { add => _messages.GeyserStateReceived += value; remove => _messages.GeyserStateReceived -= value; }

	public void FireGeyserStateReceived(IReadOnlyList<GeyserStateEntryMsg> geysers) => _messages.FireGeyserStateReceived(geysers);

	public void SendGeyserStateSnapshot(IReadOnlyList<GeyserStateEntryMsg> geysers) => _messages.SendGeyserStateSnapshot(geysers);

	public event Action<ulong, int, int, ushort>? BlockPlacedReceived { add => _messages.BlockPlacedReceived += value; remove => _messages.BlockPlacedReceived -= value; }

	public void FireBlockPlacedReceived(ulong sender, int x, int y, ushort block) => _messages.FireBlockPlacedReceived(sender, x, y, block);

	public void SendBlockPlacedReport(int x, int y, ushort block) => _messages.SendBlockPlacedReport(x, y, block);

	public void BroadcastBlockPlaced(ulong excludeSteamId, int x, int y, ushort block) => _messages.BroadcastBlockPlaced(excludeSteamId, x, y, block);

	public void SendBlockPlacedCorrection(ulong targetSteamId, int x, int y, ushort block) => _messages.SendBlockPlacedCorrection(targetSteamId, x, y, block);

	public void ResetPendingBlockReports() => _messages.ResetPendingBlockReports();

	public event Action<NetVector2, float, bool, bool>? BuildingEntityDamagedReceived { add => _messages.BuildingEntityDamagedReceived += value; remove => _messages.BuildingEntityDamagedReceived -= value; }

	public void FireBuildingEntityDamagedReceived(NetVector2 pos, float damage, bool playHitSound, bool playHitFlash) => _messages.FireBuildingEntityDamagedReceived(pos, damage, playHitSound, playHitFlash);

	public void SendBuildingEntityDamaged(NetVector2 pos, float damage, bool playHitSound = true, bool playHitFlash = false) => _messages.SendBuildingEntityDamaged(pos, damage, playHitSound, playHitFlash);

	public event Action<NetVector2>? BuildingEntityOpenedReceived { add => _messages.BuildingEntityOpenedReceived += value; remove => _messages.BuildingEntityOpenedReceived -= value; }

	public void FireBuildingEntityOpenedReceived(NetVector2 pos) => _messages.FireBuildingEntityOpenedReceived(pos);

	public void SendBuildingEntityOpened(NetVector2 pos) => _messages.SendBuildingEntityOpened(pos);

	public void ReportBlockState(int x, int y, ushort block) => _messages.ReportBlockState(x, y, block);

	public void RemoveBlockState(int x, int y) => _messages.RemoveBlockState(x, y);

	public void ResetDamagedBlocks() => ResetWorldLayerTables();

	/// <summary>
	/// Host only: a new world layer is generating — every world-domain table
	/// resets. The block/damage/trap tables live in <see cref="WorldStateMessageService"/>;
	/// the runtime-created entity table is the sibling registration table of the
	/// same generation boundary (its entities are gone with the old scene).
	/// </summary>
	private void ResetWorldLayerTables()
	{
		_messages.ResetDamagedBlocks();
		_channels.ResetRuntimeEntities();
	}

	public void SendBlockStateSnapshot(ulong targetSteamId) => _messages.SendBlockStateSnapshot(targetSteamId);

	public void ReportBlockDamage(int x, int y, float damage) => _messages.ReportBlockDamage(x, y, damage);

	public void RemoveBlockDamage(int x, int y) => _messages.RemoveBlockDamage(x, y);

	public void SendBlockDamageSnapshot(ulong targetSteamId) => _messages.SendBlockDamageSnapshot(targetSteamId);

	public event Action<IReadOnlyList<BlockDamageEntryMsg>>? BlockDamageSnapshotReceived { add => _messages.BlockDamageSnapshotReceived += value; remove => _messages.BlockDamageSnapshotReceived -= value; }

	public void FireBlockDamageSnapshotReceived(IReadOnlyList<BlockDamageEntryMsg> entries) => _messages.FireBlockDamageSnapshotReceived(entries);

	public void SetRadiationLineState(RadiationLineStateMsg state) => _messages.SetRadiationLineState(state);

	public void BroadcastRadiationLineState(RadiationLineStateMsg state) => _messages.BroadcastRadiationLineState(state);

	public void SendRadiationLineState(ulong targetSteamId) => _messages.SendRadiationLineState(targetSteamId);

	public event Action<RadiationLineStateMsg>? RadiationLineStateReceived { add => _messages.RadiationLineStateReceived += value; remove => _messages.RadiationLineStateReceived -= value; }

	public void FireRadiationLineStateReceived(RadiationLineStateMsg state) => _messages.FireRadiationLineStateReceived(state);

	public void SendWorldJoin(bool isTutorial) => _messages.SendWorldJoin(isTutorial);

	public void SendWorldJoinTo(ulong steamId) => _messages.SendWorldJoinTo(steamId);

	public void PublishWorldParams(WorldStartParams parameters)
	{
		CommitRunBaseline(parameters);
		_messages.PublishWorldParams(parameters);
	}

	private void CommitRunBaseline(WorldStartParams parameters)
	{
		var current = _kernelAuthority.QueryRun();
		var runId = current?.RunId ?? _kernelAuthority.CreateCheckpoint().RunEpoch.Value;
		var layerIndex = current is null ? 0 : current.LayerIndex + 1;
		var run = WorldRunStateMapper.ToRunState(runId, parameters, layerIndex);

		if (current is null)
		{
			if (!_kernelAuthority.TryStartRun(_session.LocalSteamId, run, out _, out var rejection))
			{
				_log.LogWarning("Kernel run start rejected: {Reason} ({Message}).", rejection!.Reason, rejection.Message);
				return;
			}

			_log.LogInformation("Committed kernel run start (run {RunId}, {StateBytes} RNG bytes).",
				runId, parameters.RandomState.Length);
		}
		else
		{
			if (!_kernelAuthority.TryAdvanceLayer(_session.LocalSteamId, run, out _, out var rejection))
			{
				_log.LogWarning("Kernel layer advance rejected: {Reason} ({Message}).", rejection!.Reason, rejection.Message);
				return;
			}

			_log.LogInformation("Committed kernel layer advance (run {RunId}, layer {Layer}).",
				runId, layerIndex);
		}
	}

	private void OnRunBatchCommitted(CommittedBatch batch) => RunBatchProjection(batch);

	private void OnRunBatchApplied(CommittedBatch batch) => RunBatchProjection(batch);

	private void RunBatchProjection(CommittedBatch batch)
	{
		_projectionHealth.Run("run", batch.GlobalRevision, () =>
		{
			foreach (var @event in batch.Events)
			{
				switch (@event)
				{
					case RunStartedEvent started:
						ApplyRunProjection(started.Run);
						break;
					case RunAdvancedEvent advanced:
						ApplyRunProjection(advanced.Run);
						break;
				}
			}
		});
	}

	private void OnRunCheckpointRestored(GameCheckpoint checkpoint)
	{
		_projectionHealth.Run("run", checkpoint.GlobalRevision, () =>
		{
			if (checkpoint.Run is not null)
			{
				ApplyRunProjection(checkpoint.Run);
			}
		});
	}

	private void RebuildRunFromKernel()
	{
		var run = _kernelAuthority.QueryRun();
		if (run is not null)
		{
			ApplyRunProjection(run);
		}
		else
		{
			WorldParams = null;
			_log.LogInformation("[RunProjection] kernel run is null; cleared the world-start projection.");
		}

		_log.LogDebug("[RunProjection] rebuilt from kernel at revision {Revision}.", _kernelAuthority.CurrentGlobalRevision);
	}

	private void ApplyRunProjection(RunState run)
	{
		WorldParams = WorldRunStateMapper.ToWorldStartParams(run);
		_log.LogInformation("Projected kernel run baseline (run {RunId}, layer {Layer}, {StateBytes} RNG bytes).",
			run.RunId, run.LayerIndex, run.RandomState.Length);
	}

	public void SendBlockDamaged(NetVector2 worldPos, float damage, bool metalBonus, IReadOnlyList<BlockDropEntryMsg>? drops, IReadOnlyList<TrapDropEntryMsg>? buildingDrops) => _messages.SendBlockDamaged(worldPos, damage, metalBonus, drops, buildingDrops);

	public void BroadcastBlockDamaged(ulong excludeSteamId, NetVector2 worldPos, float damage, bool metalBonus, IReadOnlyList<BlockDropEntryMsg>? drops, IReadOnlyList<TrapDropEntryMsg>? buildingDrops) => _messages.BroadcastBlockDamaged(excludeSteamId, worldPos, damage, metalBonus, drops, buildingDrops);
}
