using System;
using System.Collections.Generic;
using CasualtiesUnknownOnline.GameState.Domains.Fluids;
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
public sealed partial class WorldService : IWorldControl, IWorldFactSource, IDisposable, ISessionReset
{
	private readonly ISessionControl _session;
	private readonly WorldChannelRelay _channels;
	private readonly WorldStateMessageService _messages;
	private readonly BlockReportChannel _blockReports;
	private readonly WorldFactLifecycle _facts;
	private readonly GuestReportFallbacks _reportFallbacks;
	private readonly WorldRunProjection _runProjection;
	private readonly ItemKernelAuthority _kernelAuthority;
	private readonly KernelWorldGenerationSource _generations;
	private readonly ILogger<WorldService> _log;
	private readonly IWorldItemLayerReset _itemLayerReset;
	private readonly LayerScopedTableReset _layerTables;
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
		INativeWorldFacts? nativeWorldFacts,
		ItemKernelAuthority kernelAuthority,
		IWorldItemLayerReset itemLayerReset,
		FluidKernelProjection fluidKernel,
		FluidKernelReadProjection fluidKernelRead,
		ProjectionHealthCoordinator projectionHealth)
	{
		_session = session;
		_channels = new WorldChannelRelay(eventChannel, runtimeEntityChannel, tradeChannel, speechChannel, chatChannel, locationPingChannel);

		// The message surface is this facade's own collaborator, not a DI singleton:
		// the world-fact lifecycle (which the save layer resolves) is built over the
		// SAME instance, so both see one set of tables.
		_generations = new KernelWorldGenerationSource(kernelAuthority);
		_messages = new WorldStateMessageService(session, sender, log, eventChannel, _generations);
		_log = log;
		_blockReports = new BlockReportChannel(session, sender, nativeWorldFacts, new KernelWorldGenerationSource(kernelAuthority), log);
		_facts = new WorldFactLifecycle(_messages, log);
		_reportFallbacks = new GuestReportFallbacks(session);
		_startGate = new WorldStartGate(session, sender, time, log);
		_kernelAuthority = kernelAuthority;
		_itemLayerReset = itemLayerReset;
		_layerTables = new LayerScopedTableReset(session, kernelAuthority, log);
		_fluidKernel = fluidKernel;
		_fluidKernelRead = fluidKernelRead;
		_projectionHealth = projectionHealth;
		_runProjection = new WorldRunProjection(session, kernelAuthority, projectionHealth, worldParams => WorldParams = worldParams, log);
		_projectionHealth.Register(new ProjectionDomain("run", RebuildRunFromKernel, () => _kernelAuthority.CurrentGlobalRevision));

		_kernelAuthority.BatchApplied += _runProjection.OnBatch;
		_kernelAuthority.BatchCommitted += _runProjection.OnBatch;
		_kernelAuthority.CheckpointRestored += _runProjection.OnCheckpointRestored;
		session.SessionEnded += ResetSessionState;
	}

	public void SetHostRunPending(bool pending) => _startGate.SetHostRunPending(pending);

	public void FireWorldReadyReceived() => WorldReadyReceived?.Invoke();

	// ---- Start gate lifecycle (WorldStartGate owns the state) ----

	public bool StartStartGate() => _startGate.Arm();

	public void NotifyMemberInWorld(ulong steamId) => _startGate.NotifyMemberInWorld(steamId);

	public void AnswerRepeatInWorld(ulong steamId) => _startGate.AnswerRepeatInWorld(steamId);

	public void MaybeForceStartGate() => _startGate.PumpTimeout();

	public bool StartGateActive => _startGate.Active;

	public int StartGateRemainingMs => _startGate.RemainingMs;

	// ---- Guest block-report fallback (audit gap W1) ----

	/// <summary>Test/observability seam (InternalsVisibleTo): how many unacknowledged guest block reports are outstanding.</summary>
	internal int PendingBlockReportCount => _blockReports.PendingBlockReportCount;

	/// <summary>Every guest report channel's time edge (driven by <see cref="WorldReportFallbackPump"/>): each table re-sends its outstanding set once ITS window elapses — they fill and drain independently.</summary>
	internal void PumpReportFallbacks(long nowMs) =>
		_reportFallbacks.Pump(
			nowMs,
			_blockReports.PendingBlockReportCount,
			_blockReports.ResendPendingBlockReports,
			_blockReports.PendingBlockDamageReportCount,
			_blockReports.ResendPendingBlockDamageReports,
			_blockReports.GuestReports.PendingBreakDropCount,
			_blockReports.GuestReports.ResendBreakDrops);

	/// <summary>Test/observability seam (InternalsVisibleTo): how many unacknowledged guest partial-damage reports are outstanding.</summary>
	internal int PendingBlockDamageReportCount => _blockReports.PendingBlockDamageReportCount;

	/// <summary>Test/observability seam (InternalsVisibleTo): how many unacknowledged guest break-drop sets are outstanding.</summary>
	internal int PendingBreakDropReportCount => _blockReports.GuestReports.PendingBreakDropCount;

	// ---- Session reset ----

	public void ResetSessionState()
	{
		_startGate.Reset();
		_reportFallbacks.Reset();
		_channels.ResetRuntimeEntities();
		_channels.ResetPendingEntityReports();
		WorldParams = null;
		// The session that owned a pending restore is gone: the live world will
		// never consume it, and leaving the marker set would make the NEXT run's
		// first generation look like the one the cut was restored for.
		_facts.ClearPendingLiveReplay();
		_messages.ResetSessionState();
		_blockReports.ResetPendingReports();
	}


	/// <summary>
	/// The world facts the kernel does not own, exposed for the save system's cut
	/// and restore. The facade implements the port and delegates to the
	/// world-fact lifecycle, which sequences capture and the reset-then-apply
	/// restore over the tables the message surface owns — this type holds none of
	/// them itself. The GAME's own tables are deliberately NOT part of this port:
	/// only the adapter can read them, and they travel through
	/// <see cref="INativeWorldFacts"/>.
	/// </summary>
	public IReadOnlyList<BlockStateEntryMsg> CaptureBlockStates() => _facts.CaptureBlockStates();

	/// <inheritdoc cref="CaptureBlockStates"/>
	public RadiationLineStateMsg? CaptureRadiationLine() => _facts.CaptureRadiationLine();

	/// <inheritdoc cref="CaptureBlockStates"/>
	public WorldFactApplyReport ApplyFacts(
		IReadOnlyList<BlockStateEntryMsg> blockStates,
		RadiationLineStateMsg? radiationLine,
		ulong restoreSequence) =>
		_facts.ApplyFacts(blockStates, radiationLine, restoreSequence);

	/// <inheritdoc cref="CaptureBlockStates"/>
	public bool HasPendingLiveReplay => _facts.HasPendingLiveReplay;

	/// <inheritdoc cref="CaptureBlockStates"/>
	public ulong AppliedRestoreSequence => _facts.AppliedRestoreSequence;

	/// <inheritdoc cref="CaptureBlockStates"/>
	public void ClearPendingLiveReplay() => _facts.ClearPendingLiveReplay();

	public void Dispose()
	{
		_kernelAuthority.BatchApplied -= _runProjection.OnBatch;
		_kernelAuthority.BatchCommitted -= _runProjection.OnBatch;
		_kernelAuthority.CheckpointRestored -= _runProjection.OnCheckpointRestored;
		_session.SessionEnded -= ResetSessionState;
		_reportFallbacks.Unbind();
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

	public void ReportEntitySpawnUnmaterialized(ulong sender, EntitySpawnedMsg msg) => _channels.ReportEntitySpawnUnmaterialized(sender, msg);

	/// <summary>Guest: the host rejected a creation this side reported — the channel drops the pending re-report, the adapter destroys the local copy.</summary>
	public void FireRuntimeEntityRejectedReceived(ulong sender, RuntimeEntityRejectedMsg msg) =>
		_channels.FireRuntimeEntityRejectedReceived(sender, msg);

	public event Action<RuntimeEntityKey, RuntimeEntityRejectReason>? RuntimeEntityRejectedReceived { add => _channels.RuntimeEntityRejectedReceived += value; remove => _channels.RuntimeEntityRejectedReceived -= value; }

	public void SendEntitySpawned(EntitySpawnedMsg msg) => _channels.SendEntitySpawned(msg);

	/// <summary>Host only: send the accepted runtime-entity creation table to one member (world entry, or the 60 s cycle).</summary>
	public void SendRuntimeEntitySnapshot(ulong targetSteamId) => _channels.SendRuntimeEntitySnapshot(targetSteamId);

	/// <summary>Guest: the host's absolute runtime-entity table arrived — apply every entry (create missing, bind existing by creation key) and acknowledge the matching pending reports.</summary>
	public void FireRuntimeEntitySnapshotReceived(ulong sender, RuntimeEntitySnapshotMsg snapshot) =>
		_channels.FireRuntimeEntitySnapshotReceived(sender, snapshot);

	/// <summary>Either side: a runtime-created entity's local copy died — drop its accepted record (host) or its pending re-report (guest).</summary>
	public void ReportRuntimeEntityDestroyed(RuntimeEntityKey key) => _channels.ReportRuntimeEntityDestroyed(key);

	/// <summary>Guest: a new world/layer baseline was applied — drop every unacknowledged creation report from the previous world.</summary>
	public void ResetPendingEntityReports() => _channels.ResetPendingEntityReports();

	/// <summary>Host only: a new world layer is generating — the accepted runtime-entity creation table starts empty again.</summary>
	public void ResetRuntimeEntities() => _channels.ResetRuntimeEntities();

	public void ReportOpenedEntity(float x, float y) => _channels.ReportOpenedEntity(x, y);

	public void ReportBuildingEntityHealth(float x, float y, float health) => _channels.ReportBuildingEntityHealth(x, y, health);

	public void ReportTrapLayout(EntityEventKind kind, float x, float y, string prefabName, RuntimeEntityKeyMsg? creationKey = null) => _channels.ReportTrapLayout(kind, x, y, prefabName, creationKey);

	/// <summary>Host only: replace the whole trap-layout table with a fresh scan of the live scene (the in-session repair re-derives it before sending). Returns false when the empty-scan fail-safe refused it.</summary>
	public bool ReplaceTrapLayout(IReadOnlyList<TrapLayoutEntryMsg> entries) => _channels.ReplaceTrapLayout(entries);

	public void SendTrapLayoutSnapshot(ulong targetSteamId) => _channels.SendTrapLayoutSnapshot(targetSteamId);

	public event Action<IReadOnlyList<TrapLayoutEntryMsg>>? TrapLayoutReceived { add => _channels.TrapLayoutReceived += value; remove => _channels.TrapLayoutReceived -= value; }

	public void FireTrapLayoutReceived(ulong sender, WorldGenerationMsg? generation, IReadOnlyList<TrapLayoutEntryMsg> entries) => _channels.FireTrapLayoutReceived(sender, generation, entries);

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

	public event Action<ulong, int, int, float, bool, IReadOnlyList<BlockDropEntryMsg>?, IReadOnlyList<TrapDropEntryMsg>?, WorldGenerationRelation>? BlockDamagedReceived { add => _blockReports.BlockDamagedReceived += value; remove => _blockReports.BlockDamagedReceived -= value; }

	public void FireBlockDamagedReceived(ulong sender, int x, int y, float damage, bool metalBonus, IReadOnlyList<BlockDropEntryMsg>? drops, IReadOnlyList<TrapDropEntryMsg>? buildingDrops, float contribution, WorldGenerationMsg? generation) => _blockReports.FireBlockDamagedReceived(sender, x, y, damage, metalBonus, drops, buildingDrops, contribution, generation);

	public event Action<bool>? WorldJoinReceived { add => _messages.WorldJoinReceived += value; remove => _messages.WorldJoinReceived -= value; }

	public void FireWorldJoinReceived(bool isTutorial) => _messages.FireWorldJoinReceived(isTutorial);

	public event Action? WorldSnapshotCompleteReceived { add => _messages.WorldSnapshotCompleteReceived += value; remove => _messages.WorldSnapshotCompleteReceived -= value; }

	public void FireWorldSnapshotCompleteReceived() => _messages.FireWorldSnapshotCompleteReceived();

	public void SendWorldSnapshotComplete(ulong targetSteamId) => _messages.SendWorldSnapshotComplete(targetSteamId);

	/// <summary>Host: the run/layer clocks the adapter read off the live world (stamped with the kernel run baseline's generation when they are sent).</summary>
	public RunClockFacts? RunFacts
	{
		get => _messages.RunFacts;
		set => _messages.RunFacts = value;
	}

	public void SendRunFacts(ulong targetSteamId) => _messages.SendRunFacts(targetSteamId);

	public void PublishRunFacts(RunClockFacts facts) => _messages.RunFacts = facts;

	/// <summary>
	/// Guest: the host's run/layer clocks arrived. The CLOCK is applied whenever it advances
	/// (run-scoped, so a value behind can only be an older message), while the LAYER TIMER and
	/// its LIMIT are dropped when the stamp names a DIFFERENT layer of a run this side already
	/// knows — and are applied when it matches, or when it cannot be compared at all
	/// (<see cref="WorldGenerationRelation.Unknown"/>: no baseline yet, and the only sender on
	/// this connection is the host, so refusing would leave a joining member's radiation timer
	/// at zero until its own baseline arrives). The monotone write guard, not the stamp, is
	/// what keeps either value from moving backwards.
	/// </summary>
	public void FireRunFactsReceived(RunFactsMsg facts)
	{
		var reported = new WorldGenerationMsg { RunEpoch = facts.RunEpoch, LayerIndex = facts.LayerIndex };
		var relation = WorldReportGeneration.Relate(_generations.Current, reported);
		var layerTimerApplies = relation is WorldGenerationRelation.Current or WorldGenerationRelation.Unknown;

		// Debug, not Warn: a legitimate layer transition produces a window in which the host
		// already stamps the NEW generation while this side's baseline is still the old one, so
		// a normal switch can mismatch on every entry/repair send inside that window.
		if (!layerTimerApplies)
		{
			_log.LogDebug(
				"Dropped the run clock message's layer timer ({Reported}; this side is {Mine}) — its clock is still applied if it advances, the layer timer is not.",
				WorldReportGeneration.Describe(reported), WorldReportGeneration.Describe(_generations.Current));
		}

		_messages.FireRunFactsReceived(facts, layerTimerApplies);
	}

	public event Action<RunFactsMsg, bool>? RunFactsReceived { add => _messages.RunFactsReceived += value; remove => _messages.RunFactsReceived -= value; }

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

	public event Action<ulong, int, int, ushort, bool, WorldGenerationRelation>? BlockPlacedReceived { add => _blockReports.BlockPlacedReceived += value; remove => _blockReports.BlockPlacedReceived -= value; }

	public void FireBlockPlacedReceived(ulong sender, int x, int y, ushort block, bool playerBreak, WorldGenerationMsg? generation) => _blockReports.FireBlockPlacedReceived(sender, x, y, block, playerBreak, generation);

	public void SendBlockPlacedReport(int x, int y, ushort block, bool playerBreak) => _blockReports.SendBlockPlacedReport(x, y, block, playerBreak);

	public void BroadcastBlockPlaced(ulong excludeSteamId, int x, int y, ushort block, bool playerBreak) => _blockReports.BroadcastBlockPlaced(excludeSteamId, x, y, block, playerBreak);

	public void SendBlockPlacedCorrection(ulong targetSteamId, int x, int y, ushort block) => _blockReports.SendBlockPlacedCorrection(targetSteamId, x, y, block);

	// The guest report-recovery surface (the three channels of the W1/W2 family)
	// lives in the WorldService.ReportRecovery partial — it is one cohesive block
	// of adapter-facing pass-throughs, and this file is at the 600-line gate.

	/// <summary>Host only: a guest's absolute partial-damage report arrived (with its world/layer stamp) — merge it through the native port and answer every reported cell authoritatively, unless it belongs to another generation.</summary>
	public void HandleBlockDamageReport(ulong sender, IReadOnlyList<BlockDamageEntryMsg> entries, WorldGenerationMsg? generation) => _blockReports.HandleBlockDamageReport(sender, entries, generation);

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
	/// resets. The block/damage/radiation tables live behind the world-fact
	/// lifecycle (which the save layer also reads and rewrites); the
	/// runtime-created entity table is the sibling registration table of the same
	/// generation boundary (its entities are gone with the old scene).
	///
	/// This is the SEAM of the layer-boundary reset family; the RULE that decides the
	/// family's membership — and the reason the two kernel tables it owns are reset at
	/// this seam and not at the layer-end cut's kernel commit — lives in
	/// <see cref="LayerScopedTableReset"/>.
	/// </summary>
	private void ResetWorldLayerTables()
	{
		_facts.ResetWorldDomainTables();
		_channels.ResetRuntimeEntities();
		_itemLayerReset.ResetForNewLayer();
		_layerTables.Reset();

		// The partial-damage accounting belongs to the layer too: this side's
		// outstanding contributions and the host's per-sender ledger describe cells
		// of the world that is being replaced (a fresh block at the same cell must
		// start from zero). The guest's own boundary in WorldParamsService calls
		// this as well; the reset is idempotent.
		_blockReports.ResetPendingBlockDamageReports();
	}

	public void SendBlockStateSnapshot(ulong targetSteamId) => _messages.SendBlockStateSnapshot(targetSteamId);

	public void SendBlockDamageSnapshot(ulong targetSteamId) => _blockReports.SendBlockDamageSnapshot(targetSteamId);

	public event Action<IReadOnlyList<BlockDamageEntryMsg>>? BlockDamageSnapshotReceived { add => _blockReports.BlockDamageSnapshotReceived += value; remove => _blockReports.BlockDamageSnapshotReceived -= value; }

	public void FireBlockDamageSnapshotReceived(IReadOnlyList<BlockDamageEntryMsg> entries, WorldGenerationMsg? generation, bool answersReport) => _blockReports.FireBlockDamageSnapshotReceived(entries, generation, answersReport);

	public void SetRadiationLineState(RadiationLineStateMsg state) => _messages.SetRadiationLineState(state);

	public void BroadcastRadiationLineState(RadiationLineStateMsg state) => _messages.BroadcastRadiationLineState(state);

	public void SendRadiationLineState(ulong targetSteamId) => _messages.SendRadiationLineState(targetSteamId);

	public event Action<RadiationLineStateMsg>? RadiationLineStateReceived { add => _messages.RadiationLineStateReceived += value; remove => _messages.RadiationLineStateReceived -= value; }

	public void FireRadiationLineStateReceived(RadiationLineStateMsg state) => _messages.FireRadiationLineStateReceived(state);

	// The host's own kernel run epoch stamps the instruction: the member is told which
	// run it is entering, and it refuses checkpoint sets that belong to another one.
	public void SendWorldJoin(bool isTutorial) => _messages.SendWorldJoin(isTutorial, _kernelAuthority.CurrentRunEpoch.Value);

	public void SendWorldJoinTo(ulong steamId) => _messages.SendWorldJoinTo(steamId, _kernelAuthority.CurrentRunEpoch.Value);

	public void PublishWorldParams(WorldStartParams parameters)
	{
		_runProjection.CommitBaseline(parameters);
		_messages.PublishWorldParams(parameters);
	}

	private void RebuildRunFromKernel() => _runProjection.Rebuild(() => WorldParams = null);

	public void SendBlockDamaged(int x, int y, float damage, bool metalBonus, float contribution, IReadOnlyList<BlockDropEntryMsg>? drops, IReadOnlyList<TrapDropEntryMsg>? buildingDrops) => _blockReports.SendBlockDamaged(x, y, damage, metalBonus, contribution, drops, buildingDrops);

	public void BroadcastBlockDamaged(ulong excludeSteamId, int x, int y, float damage, bool metalBonus, IReadOnlyList<BlockDropEntryMsg>? drops, IReadOnlyList<TrapDropEntryMsg>? buildingDrops) => _blockReports.BroadcastBlockDamaged(excludeSteamId, x, y, damage, metalBonus, drops, buildingDrops);
}
