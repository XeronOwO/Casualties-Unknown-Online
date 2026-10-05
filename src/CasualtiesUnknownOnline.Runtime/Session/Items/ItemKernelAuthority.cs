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
	/// ONE container report — the parent's own fact plus where every child sits
	/// inside it — committed as ONE atomic batch. This is the same command the
	/// guest's <c>ItemContainerSync</c> report maps to on the wire, so the owner's
	/// local scene and a remote report write the kernel the same way.
	///
	/// The child LOCATIONS are the load-bearing half. The carried fact a peer
	/// rebuilds from this batch lists the parent's contents from the kernel's
	/// contained children, so a batch that only carried the parent's data reaches
	/// the peers as an EMPTY container while the moved child keeps its previous
	/// kernel location: the clone fact table then warns "nested container contents
	/// changed" AND "left the inventory" on the next snapshot, because the event
	/// announced neither half of the move.
	/// </summary>
	public bool TrySyncContainerFacts(ulong actor, CharacterItemMsg parent, out CommittedBatch? batch, out Rejection? rejection)
	{
		var command = new SyncContainerItemsCommand(
			NextOperation(),
			new ActorId(actor),
			_runEpoch,
			AuthorityKind.OwnerPredictedHostValidated,
			new ItemIdentity(parent.InstanceId, parent.ItemId),
			ToKernelData(parent),
			FlattenChildren(parent));
		var dropped = ChildrenTheReportDrops(parent);
		var accepted = TryExecute(command, actor, "container-sync", out batch, out rejection);
		if (accepted && dropped.Count > 0)
		{
			// The report is the container's truth, so a child it does not name is no
			// longer inside — and the decision takes it to Terminal (ReplacedBy),
			// which no later command may revive ("terminal child … cannot re-enter
			// container" rejects the WHOLE container sync). That is the right verdict
			// for a container that really is empty, and the wrong one for a child
			// that merely moved without its own relocation fact, so the branch is
			// observable instead of silent: a session that never sees this line has
			// no stale contained record, and one that does has the ids to chase.
			_log.LogWarning("[ContainerSync] the report for {ParentId} no longer names {Count} item(s) the kernel records inside it ({Items}) — they go Terminal (ReplacedBy); a child that left the container without its own relocation fact dies here.",
				parent.InstanceId, dropped.Count, string.Join(", ", dropped));
		}

		return accepted;
	}

	/// <summary>
	/// The report's own stale set, read before the command runs so the ids can be
	/// logged: every kernel item that is a descendant of this parent and that the
	/// report does not name anywhere in its subtree. Mirrors the walk
	/// <c>ItemDomainModule.DecideSyncContainer</c> destroys, which is the point —
	/// the log names exactly what that branch is about to make Terminal.</summary>
	private List<ulong> ChildrenTheReportDrops(CharacterItemMsg parent)
	{
		var dropped = new List<ulong>();
		var named = new HashSet<ulong>();
		CollectReportedIds(parent, named, isRoot: true);
		foreach (var item in _kernel.QueryItems().Values)
		{
			var id = item.Identity.InstanceId;
			if (id == parent.InstanceId || named.Contains(id))
			{
				continue;
			}

			if (ItemLocationChain.IsDescendantOf(id, parent.InstanceId, _kernel.FindItem))
			{
				dropped.Add(id);
			}
		}

		return dropped;
	}

	private static void CollectReportedIds(CharacterItemMsg item, HashSet<ulong> ids, bool isRoot)
	{
		if (!isRoot && item.InstanceId != 0)
		{
			ids.Add(item.InstanceId);
		}

		foreach (var child in item.Contents)
		{
			CollectReportedIds(child, ids, isRoot: false);
		}
	}

	/// <summary>The report's recursive child walk, in report order: every child
	/// keeps the parent linkage the report gave it, so the domain's decision sees
	/// the whole subtree in one command. Depth is the report's, not the kernel's —
	/// an unbound (id 0) child is carried like the wire path carries it and is
	/// skipped by the decision.</summary>
	private static IReadOnlyList<ContainerChildFact> FlattenChildren(CharacterItemMsg parent)
	{
		var children = new List<ContainerChildFact>();
		AppendChildren(parent, children);
		return children;
	}

	private static void AppendChildren(CharacterItemMsg parent, List<ContainerChildFact> children)
	{
		foreach (var child in parent.Contents)
		{
			children.Add(new ContainerChildFact(child.InstanceId, child.ItemId, parent.InstanceId, ToKernelData(child)));
			AppendChildren(child, children);
		}
	}

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
