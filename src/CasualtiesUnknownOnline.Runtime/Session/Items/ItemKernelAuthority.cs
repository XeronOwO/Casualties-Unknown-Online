using System;
using System.Collections.Generic;
using CasualtiesUnknownOnline.Application.Kernel;
using CasualtiesUnknownOnline.GameState;
using CasualtiesUnknownOnline.GameState.Domains.Entities;
using CasualtiesUnknownOnline.GameState.Domains.Fluids;
using CasualtiesUnknownOnline.GameState.Domains.Items;
using CasualtiesUnknownOnline.GameState.Domains.Players;
using CasualtiesUnknownOnline.GameState.Domains.World;
using CasualtiesUnknownOnline.GameState.Domains.WorldEntities;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using Microsoft.Extensions.Logging;

namespace CasualtiesUnknownOnline.Runtime.Session.Items;

/// <summary>
/// Phase B item authority: the host's kernel-backed item fact store. Every
/// persistent item mutation on the host is expressed as a typed kernel command;
/// the old <see cref="WorldItemTable"/> and transfer table become projections
/// that are updated only after an accepted batch. The authority owns the
/// deterministic kernel, the run epoch, and the operation-id counter.
/// </summary>
public sealed class ItemKernelAuthority(ILogger<ItemKernelAuthority> log)
	: IKernelItemFacts, IKernelCommandExecution, IKernelCheckpointSource, IKernelBatchApplication, ISessionReset
{
	private readonly ILogger<ItemKernelAuthority> _log = log;
	private readonly HashSet<OperationId> _appliedOperations = [];
	private GameStateKernel _kernel = new(new RunEpoch(1));

	/// <summary>The kernel store owns the run epoch; a restored checkpoint re-identifies the run.</summary>
	private RunEpoch _runEpoch => _kernel.RunEpoch;
	private ulong _nextOperation = 1;


	/// <summary>Start a fresh authority epoch after a session/run reset.</summary>
	public void ResetSessionState()
	{
		_kernel = new GameStateKernel(new RunEpoch(_kernel.RunEpoch.Value + 1));
		_nextOperation = 1;
		_appliedOperations.Clear();
	}

	/// <summary>Raised after any accepted kernel batch commits. The Phase C protocol service broadcasts this to guests.</summary>
	public event Action<CommittedBatch>? BatchCommitted;

	/// <summary>Raised after a kernel batch committed through the external wire-command entry point. Used by host projections to refresh local state without double-projecting local native writes.</summary>
	public event Action<CommittedBatch>? ExternalBatchCommitted;

	/// <summary>Raised after a remote/replay batch is applied to this authority's kernel (guest side).</summary>
	public event Action<CommittedBatch>? BatchApplied;

	/// <summary>Raised after a checkpoint restore replaces the kernel state.</summary>
	public event Action<GameCheckpoint>? CheckpointRestored;

	// ===== Query =====

	public ItemState? FindItem(ulong instanceId) => _kernel.FindItem(instanceId);

	public IReadOnlyDictionary<ulong, ItemState> QueryItems() => _kernel.QueryItems();

	public RunState? QueryRun() => _kernel.QueryRun();

	public WorldEntityState? QueryWorldEntities() => _kernel.QueryWorldEntities();

	public PlayerStateTable? QueryPlayers() => _kernel.QueryPlayers();

	public EnemyStateTable? QueryEnemies() => _kernel.QueryEnemies();

	public FluidStateTable? QueryFluids() => _kernel.QueryFluids();

	public GameCheckpoint CreateCheckpoint() => _kernel.CreateCheckpoint();

	public RestoreResult Restore(GameCheckpoint checkpoint)
	{
		var result = _kernel.Restore(checkpoint);
		if (!result.Success)
		{
			return result;
		}

		RestoreSequence++;
		_appliedOperations.Clear();
		CheckpointRestored?.Invoke(checkpoint);
		return result;
	}

	/// <summary>Replay/guest side: apply an already-committed batch idempotently.</summary>
	public ApplyResult Apply(CommittedBatch batch)
	{
		if (_appliedOperations.Contains(batch.OperationId))
		{
			return ApplyResult.Ok();
		}

		var result = _kernel.Apply(batch);
		if (!result.Success)
		{
			return result;
		}

		_appliedOperations.Add(batch.OperationId);
		BatchApplied?.Invoke(batch);
		return result;
	}

	/// <summary>The current authoritative global revision (checkpoint-derived).</summary>
	public ulong CurrentGlobalRevision => _kernel.CreateCheckpoint().GlobalRevision;

	/// <summary>
	/// WHICH restore produced the kernel state this authority holds: bumped by every
	/// successful <see cref="Restore"/>, 0 while the kernel was never restored.
	///
	/// It is the identity of a restore ATTEMPT. The restore's live-world halves are
	/// not written at the Continue click — they are armed here and written later (the
	/// world-fact tables and the adapter's native handover at the world-entry seam, the
	/// restored world-item set at the generation reconcile) — so every arm stamps
	/// itself with this value when it is armed, and the save layer's restore account
	/// (<c>WorldRestoreAudit</c>) is opened with the same value. A contribution that
	/// reaches an account opened for a LATER attempt is then attributable to the
	/// attempt that armed it instead of being counted toward the new one.
	///
	/// The counter is per authority (one per process) and counts ATTEMPTS, not
	/// archives: restoring the same snapshot twice makes two different writes into the
	/// live world, so the two must not share an identity. It is deliberately NOT reset
	/// by <see cref="ResetSessionState"/> — it only has to distinguish attempts, and a
	/// counter that restarted would let a previous session's straggler collide with the
	/// next session's first restore. A peer's checkpoint cannot move it either: the
	/// wire checkpoint path is guest-only, and a guest never opens a restore account
	/// (<c>RunSaveCoordinator.OnContinueRequested</c> refuses one).
	/// </summary>
	public ulong RestoreSequence { get; private set; }

	// ===== World / Run =====

	public bool TryStartRun(ulong actor, RunState run, out CommittedBatch? batch, out Rejection? rejection)
	{
		var command = new StartRunCommand(
			NextOperation(),
			new ActorId(actor),
			_runEpoch,
			AuthorityKind.HostOnly,
			run);
		return TryExecute(command, actor, "start-run", out batch, out rejection);
	}

	public bool TryAdvanceLayer(ulong actor, RunState run, out CommittedBatch? batch, out Rejection? rejection)
	{
		var command = new AdvanceLayerCommand(
			NextOperation(),
			new ActorId(actor),
			_runEpoch,
			AuthorityKind.HostOnly,
			run);
		return TryExecute(command, actor, "advance-layer", out batch, out rejection);
	}

	// ===== World entities (traps/buildings) =====

	public bool TryRecordTrapConsumed(ulong actor, EntityPosition position, int kind, byte extra, long triggeredAtMs, out CommittedBatch? batch, out Rejection? rejection)
	{
		var command = new RecordTrapConsumedCommand(
			NextOperation(),
			new ActorId(actor),
			_runEpoch,
			AuthorityKind.HostOnly,
			position,
			kind,
			extra,
			triggeredAtMs);
		return TryExecute(command, actor, "record-trap-consumed", out batch, out rejection);
	}

	public bool TryRecordBuildingEntityHealth(ulong actor, EntityPosition position, float health, out CommittedBatch? batch, out Rejection? rejection)
	{
		var command = new RecordBuildingEntityHealthCommand(
			NextOperation(),
			new ActorId(actor),
			_runEpoch,
			AuthorityKind.HostOnly,
			position,
			health);
		return TryExecute(command, actor, "record-building-health", out batch, out rejection);
	}

	public bool TryRecordOpenedEntity(ulong actor, EntityPosition position, out CommittedBatch? batch, out Rejection? rejection)
	{
		var command = new RecordOpenedEntityCommand(
			NextOperation(),
			new ActorId(actor),
			_runEpoch,
			AuthorityKind.HostOnly,
			position);
		return TryExecute(command, actor, "record-opened-entity", out batch, out rejection);
	}

	public bool TryResetWorldEntities(ulong actor, out CommittedBatch? batch, out Rejection? rejection)
	{
		var command = new ResetWorldEntitiesCommand(
			NextOperation(),
			new ActorId(actor),
			_runEpoch,
			AuthorityKind.HostOnly);
		return TryExecute(command, actor, "reset-world-entities", out batch, out rejection);
	}

	// ===== World items (layer boundary) =====

	/// <summary>
	/// Host only: a new layer is generating — every world-rooted item of the
	/// previous layer is gone with its scene, so the authoritative kernel drops
	/// them. Carried items (and their contents) cross the boundary untouched.
	/// The committed batch travels to the guests, so their replay kernels reset
	/// at the same boundary.
	/// </summary>
	public bool TryResetWorldItems(ulong actor, out CommittedBatch? batch, out Rejection? rejection)
	{
		var command = new ResetWorldItemsCommand(
			NextOperation(),
			new ActorId(actor),
			_runEpoch,
			AuthorityKind.HostOnly);
		return TryExecute(command, actor, "reset-world-items", out batch, out rejection);
	}

	// ===== Players =====

	public bool TryUpdatePlayerStatus(ulong actor, PlayerState state, out CommittedBatch? batch, out Rejection? rejection) =>
		TryExecute(new UpdatePlayerStatusCommand(NextOperation(), new ActorId(actor), _runEpoch, AuthorityKind.HostOnly, state), actor, "update-player-status", out batch, out rejection);

	// The player table has NO reset, and that is a design statement rather than an
	// omission: PlayerState carries the durable CROSS-LAYER facts (alive/conscious,
	// the carry relation, the limb latches, body state, skills — see PlayerState), so
	// a layer boundary must keep them. The family that DOES reset at a boundary is
	// enumerated in WorldService.ResetWorldLayerTables; players are not in it.

	public bool TrySetPlayerCarry(ulong actor, ulong carrierSteamId, ulong carriedSteamId, out CommittedBatch? batch, out Rejection? rejection) =>
		TryExecute(new SetPlayerCarryCommand(NextOperation(), new ActorId(actor), _runEpoch, AuthorityKind.HostOnly, carrierSteamId, carriedSteamId), actor, "set-player-carry", out batch, out rejection);

	public bool TryClearPlayerCarry(ulong actor, ulong carrierSteamId, ulong carriedSteamId, out CommittedBatch? batch, out Rejection? rejection) =>
		TryExecute(new ClearPlayerCarryCommand(NextOperation(), new ActorId(actor), _runEpoch, AuthorityKind.HostOnly, carrierSteamId, carriedSteamId), actor, "clear-player-carry", out batch, out rejection);

	// ===== Entities =====

	public bool TryUpsertEnemy(ulong actor, EnemyState state, out CommittedBatch? batch, out Rejection? rejection)
	{
		var command = new UpsertEnemyCommand(
			NextOperation(),
			new ActorId(actor),
			_runEpoch,
			AuthorityKind.HostOnly,
			state);
		return TryExecute(command, actor, "upsert-enemy", out batch, out rejection);
	}

	public bool TryRemoveEnemy(ulong actor, EntityId entityId, out CommittedBatch? batch, out Rejection? rejection)
	{
		var command = new RemoveEnemyCommand(
			NextOperation(),
			new ActorId(actor),
			_runEpoch,
			AuthorityKind.HostOnly,
			entityId);
		return TryExecute(command, actor, "remove-enemy", out batch, out rejection);
	}

	/// <summary>
	/// Host only: the kernel's LAYER-SCOPED enemy table drops its LIVE rows for the layer
	/// being entered. A live enemy row is a fact about the layout it stands in AND about
	/// the id the host allocated for it (<c>EnemySyncCoordinator</c> keeps allocating from
	/// a per-session counter), so a row that survived the boundary would describe an enemy
	/// of a layer the world no longer is — a fact that leaks to a late joiner's checkpoint
	/// and whose id can be re-minted for a different enemy.
	///
	/// The TOMBSTONES survive: <c>EnemyStateTable.WithoutLiveEnemies</c> states why.
	///
	/// Host-local by construction, like the world-item and world-entity resets: a remotely
	/// triggerable "wipe every enemy" is a destructive trigger no peer may have, so this
	/// command has no wire form (<see cref="KernelWireMapper"/> does not map it) and the
	/// guests converge from the committed batch and the checkpoints.
	///
	/// <paramref name="actor"/> is the LOCAL peer — the host acting as itself. It is a
	/// parameter rather than a session lookup because this authority is also driven by
	/// the save layer, which is not the session.
	/// </summary>
	public bool TryResetEnemies(ulong actor, out CommittedBatch? batch, out Rejection? rejection)
	{
		var command = new ResetEnemiesCommand(
			NextOperation(),
			new ActorId(actor),
			_runEpoch,
			AuthorityKind.HostOnly);
		return TryExecute(command, actor, "reset-enemies", out batch, out rejection);
	}

	// ===== Fluids =====

	public bool TryUpdateFluidRegion(ulong actor, FluidRegionState state, out CommittedBatch? batch, out Rejection? rejection)
	{
		var command = new UpdateFluidRegionCommand(
			NextOperation(),
			new ActorId(actor),
			_runEpoch,
			AuthorityKind.HostOnly,
			state);
		return TryExecute(command, actor, "update-fluid-region", out batch, out rejection);
	}

	/// <inheritdoc cref="TryResetEnemies"/>
	public bool TryResetFluids(ulong actor, out CommittedBatch? batch, out Rejection? rejection)
	{
		var command = new ResetFluidsCommand(
			NextOperation(),
			new ActorId(actor),
			_runEpoch,
			AuthorityKind.HostOnly);
		return TryExecute(command, actor, "reset-fluids", out batch, out rejection);
	}

	// ===== Spawn =====

	public bool TrySpawn(ulong actor, ItemIdentity identity, ItemLocation location, CharacterItemMsg item, out CommittedBatch? batch, out Rejection? rejection,
		float velocityX = 0f, float velocityY = 0f, float rotation = 0f, bool freshItemDrop = false, float angularVelocity = 0f)
	{
		var command = new SpawnItemCommand(
			NextOperation(),
			new ActorId(actor),
			_runEpoch,
			AuthorityKind.TriggerObservedHostCommitted,
			identity,
			location,
			0,
			ToKernelData(item),
			velocityX,
			velocityY,
			rotation,
			freshItemDrop,
			angularVelocity);
		return TryExecute(command, actor, "spawn", out batch, out rejection);
	}

	public bool TrySpawnCarried(ulong actor, ulong itemId, string definitionId, CharacterItemMsg item, out CommittedBatch? batch, out Rejection? rejection)
	{
		var command = new SpawnItemCommand(
			NextOperation(),
			new ActorId(actor),
			_runEpoch,
			AuthorityKind.TriggerObservedHostCommitted,
			new ItemIdentity(itemId, definitionId),
			ItemLocation.Carried(new ActorId(actor)),
			0,
			ToKernelData(item));
		return TryExecute(command, actor, "carried-spawn", out batch, out rejection);
	}

	// ===== Location transitions =====

	public bool TryPickup(ulong actor, ulong itemId, ActorId newOwner, out CommittedBatch? batch, out Rejection? rejection)
	{
		var current = _kernel.FindItem(itemId);
		if (current is null)
		{
			rejection = Rejection.Of(RejectionReason.UnknownAggregate, $"item {itemId} does not exist");
			batch = null;
			return false;
		}

		var command = new PickUpItemCommand(
			NextOperation(),
			new ActorId(actor),
			_runEpoch,
			AuthorityKind.OwnerPredictedHostValidated,
			itemId,
			newOwner,
			current.Value.Revision);
		return TryExecute(command, actor, "pickup", out batch, out rejection);
	}

	public bool TryDrop(ulong actor, ulong itemId, ItemLocation newLocation, CharacterItemMsg? item, out CommittedBatch? batch, out Rejection? rejection)
	{
		var current = _kernel.FindItem(itemId);
		if (current is null)
		{
			rejection = Rejection.Of(RejectionReason.UnknownAggregate, $"item {itemId} does not exist");
			batch = null;
			return false;
		}

		var command = new DropItemCommand(
			NextOperation(),
			new ActorId(actor),
			_runEpoch,
			AuthorityKind.OwnerPredictedHostValidated,
			itemId,
			newLocation,
			current.Value.Revision,
			item is null ? null : ToKernelData(item));
		return TryExecute(command, actor, "drop", out batch, out rejection);
	}

	public bool TryDestroy(ulong actor, ulong itemId, TerminalKind kind, out CommittedBatch? batch, out Rejection? rejection)
	{
		var current = _kernel.FindItem(itemId);
		if (current is null)
		{
			rejection = Rejection.Of(RejectionReason.UnknownAggregate, $"item {itemId} does not exist");
			batch = null;
			return false;
		}

		var command = new DestroyItemCommand(
			NextOperation(),
			new ActorId(actor),
			_runEpoch,
			AuthorityKind.HostOnly,
			itemId,
			kind,
			current.Value.Revision);
		return TryExecute(command, actor, "destroy", out batch, out rejection);
	}

	// ===== Cook =====

	public bool TryCook(ulong actor, ulong sourceItemId, ItemIdentity cookedIdentity, ItemLocation cookedLocation, CharacterItemMsg? cookedItem, out CommittedBatch? batch, out Rejection? rejection)
	{
		// Accept-first: the source may not have entered the kernel yet (the host
		// cooker is a native observation). Missing sources are ignored by the
		// domain; the cooked product still commits.
		var source = _kernel.FindItem(sourceItemId);
		var command = new CookItemCommand(
			NextOperation(),
			new ActorId(actor),
			_runEpoch,
			AuthorityKind.HostOnly,
			source?.Identity ?? new ItemIdentity(sourceItemId, ""),
			cookedIdentity,
			cookedLocation,
			cookedItem is null ? null : ToKernelData(cookedItem),
			source?.Revision ?? 0);
		return TryExecute(command, actor, "cook", out batch, out rejection);
	}

	// ===== State updates =====

	public bool TryUpdateState(ulong actor, ulong itemId, CharacterItemMsg item, out CommittedBatch? batch, out Rejection? rejection)
	{
		var current = _kernel.FindItem(itemId);
		if (current is null)
		{
			rejection = Rejection.Of(RejectionReason.UnknownAggregate, $"item {itemId} does not exist");
			batch = null;
			return false;
		}

		var command = new UpdateItemStateCommand(
			NextOperation(),
			new ActorId(actor),
			_runEpoch,
			AuthorityKind.OwnerPredictedHostValidated,
			itemId,
			ToKernelData(item),
			current.Value.Revision);
		return TryExecute(command, actor, "update-state", out batch, out rejection);
	}

	public bool TryTransfer(ulong actor, ulong itemId, ActorId newOwner, CharacterItemMsg? item, out CommittedBatch? batch, out Rejection? rejection)
	{
		var current = _kernel.FindItem(itemId);
		if (current is null)
		{
			rejection = Rejection.Of(RejectionReason.UnknownAggregate, $"item {itemId} does not exist");
			batch = null;
			return false;
		}

		var command = new TransferItemCommand(
			NextOperation(),
			new ActorId(actor),
			_runEpoch,
			AuthorityKind.OwnerPredictedHostValidated,
			itemId,
			newOwner,
			item is null ? null : ToKernelData(item),
			current.Value.Revision);
		return TryExecute(command, actor, "transfer", out batch, out rejection);
	}

	/// <summary>
	/// Reconcile a container's authoritative child items against a recursive
	/// wire/save-shaped container report. Each contained child is its own
	/// kernel item; the walk and the writes it drives live in
	/// <see cref="ItemContainerSyncWriter"/>.
	/// </summary>
	public void SyncContainerContents(ulong actor, ulong parentItemId, CharacterItemMsg parent, ActorId owner) =>
		ItemContainerSyncWriter.Sync(this, actor, parentItemId, parent, owner);

	// ===== Kernel convenience entry points (used by craft/tests) =====

	public void ObserveSpawn(ulong actor, ulong itemId, string definitionId, float x, float y) =>
		ObserveSpawn(actor, itemId, definitionId, ItemLocation.World(x, y));

	public void ObserveSpawn(ulong actor, ulong itemId, string definitionId, ItemLocation location)
	{
		var msg = new CharacterItemMsg { ItemId = definitionId, SlotIndex = -1 };
		TrySpawn(actor, new ItemIdentity(itemId, definitionId), location, msg, out _, out _);
	}

	public void ObserveCarriedSpawn(ulong actor, ulong itemId, string definitionId)
	{
		var msg = new CharacterItemMsg { ItemId = definitionId, SlotIndex = -1 };
		TrySpawnCarried(actor, itemId, definitionId, msg, out _, out _);
	}

	public void ObservePickup(ulong actor, ulong itemId) => TryPickup(actor, itemId, new ActorId(actor), out _, out _);

	public void ObserveDrop(ulong actor, ulong itemId, float x, float y, ulong parentItemId) =>
		TryDrop(actor, itemId, ItemLocation.World(x, y, parentItemId), null, out _, out _);

	public void ObserveDestroy(ulong actor, ulong itemId, TerminalKind kind = TerminalKind.Destroyed) =>
		TryDestroy(actor, itemId, kind, out _, out _);

	// ===== Diagnostics / projection hooks =====

	/// <summary>Convert a kernel item state back to the wire/save-shaped projection.</summary>
	public static CharacterItemMsg ToCharacterItem(ItemState state) => ItemKernelCodec.ToCharacterItem(state);

	/// <summary>Convert a wire item message to the kernel-owned payload.</summary>
	public static ItemData ToKernelData(CharacterItemMsg item) => ItemKernelCodec.ToKernelData(item);

	private OperationId NextOperation() => new(_nextOperation++);

	/// <summary>
	/// Execute a host-originated typed kernel command without the wire-command
	/// external-projection hook. Used by non-item host domains that need to
	/// commit journal/result facts through the same authority.
	/// </summary>
	internal RunEpoch CurrentRunEpoch => _runEpoch;

	RunEpoch IKernelCheckpointSource.CurrentRunEpoch => _runEpoch;

	internal OperationId NextOperationId() => NextOperation();

	internal bool TryExecuteHostCommand(GameCommand command, ulong actor, string label, out CommittedBatch? batch, out Rejection? rejection) =>
		TryExecute(command, actor, label, out batch, out rejection);

	/// <summary>
	/// Execute an externally supplied typed kernel command (e.g. decoded from a
	/// Phase C CommandEnvelope). This is the host's generic command entry point.
	/// </summary>
	public bool TryExecuteCommand(GameCommand command, ulong actor, out CommittedBatch? batch, out Rejection? rejection)
	{
		if (!TryExecute(command, actor, "wire-command", out batch, out rejection))
		{
			return false;
		}

		ExternalBatchCommitted?.Invoke(batch!);
		return true;
	}

	private bool TryExecute(GameCommand command, ulong actor, string label, out CommittedBatch? batch, out Rejection? rejection)
	{
		var decision = _kernel.Execute(command, new CommandContext(_runEpoch, new ActorId(actor)));
		if (!decision.IsAccepted)
		{
			batch = null;
			rejection = decision.Rejection;
			_log.LogWarning("Item kernel authority {Label} rejected: {Reason} ({Message})",
				label, rejection!.Reason, rejection.Message);
			return false;
		}

		batch = decision.Batch;
		rejection = null;
		BatchCommitted?.Invoke(batch!);
		return true;
	}
}
