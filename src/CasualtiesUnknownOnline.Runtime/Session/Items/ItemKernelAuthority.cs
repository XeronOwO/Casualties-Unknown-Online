using System;
using System.Collections.Generic;
using System.Linq;
using CasualtiesUnknownOnline.GameState;
using CasualtiesUnknownOnline.GameState.Domains.Items;
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
{
	private readonly ILogger<ItemKernelAuthority> _log = log;
	private readonly HashSet<OperationId> _appliedOperations = [];
	private GameStateKernel _kernel = new(new RunEpoch(1));
	private RunEpoch _runEpoch = new(1);
	private ulong _nextOperation = 1;

	/// <summary>Test/read-only window into the authoritative kernel state.</summary>
	internal GameStateKernel KernelForDiagnostics => _kernel;

	/// <summary>Start a fresh authority epoch after a session/run reset.</summary>
	public void ResetForSession()
	{
		_runEpoch = new RunEpoch(_runEpoch.Value + 1);
		_kernel = new GameStateKernel(_runEpoch);
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

	public GameCheckpoint CreateCheckpoint() => _kernel.CreateCheckpoint();

	public RestoreResult Restore(GameCheckpoint checkpoint)
	{
		var result = _kernel.Restore(checkpoint);
		if (!result.Success)
		{
			return result;
		}

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

	// ===== Spawn =====

	public bool TrySpawn(ulong actor, ItemIdentity identity, ItemLocation location, CharacterItemMsg item, out CommittedBatch? batch, out Rejection? rejection)
	{
		var command = new SpawnItemCommand(
			NextOperation(),
			new ActorId(actor),
			_runEpoch,
			AuthorityKind.TriggerObservedHostCommitted,
			identity,
			location,
			0,
			ToKernelData(item));
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
	/// kernel item; this method spawns missing children, updates known ones,
	/// and destroys children that left the container.
	/// </summary>
	public void SyncContainerContents(ulong actor, ulong parentItemId, CharacterItemMsg parent, ActorId owner)
	{
		var desired = new HashSet<ulong>();
		SyncChildren(actor, parentItemId, parent, owner, desired);

		var stale = _kernel.QueryItems().Values
			.Where(i => i.Location.Kind == ItemLocationKind.Contained
				&& i.Location.ParentItemId == parentItemId
				&& !desired.Contains(i.Identity.InstanceId))
			.Select(i => i.Identity.InstanceId)
			.ToList();
		foreach (var staleId in stale)
		{
			TryDestroyExternal(actor, staleId, TerminalKind.ReplacedBy);
		}
	}

	private void SyncChildren(ulong actor, ulong parentItemId, CharacterItemMsg parent, ActorId owner, HashSet<ulong> desired)
	{
		foreach (var child in parent.Contents)
		{
			if (child.InstanceId == 0)
			{
				continue;
			}

			desired.Add(child.InstanceId);
			var current = _kernel.FindItem(child.InstanceId);
			if (current is null)
			{
				var location = ItemLocation.Contained(owner, parentItemId);
				TrySpawnExternal(actor, new ItemIdentity(child.InstanceId, child.ItemId), location, child);
			}
			else if (current.Value.Location.Kind == ItemLocationKind.Contained
				&& current.Value.Location.ParentItemId == parentItemId)
			{
				TryUpdateStateExternal(actor, child.InstanceId, child);
			}

			SyncChildren(actor, child.InstanceId, child, owner, desired);
		}
	}

	private void TrySpawnExternal(ulong actor, ItemIdentity identity, ItemLocation location, CharacterItemMsg item)
	{
		var command = new SpawnItemCommand(
			NextOperation(),
			new ActorId(actor),
			_runEpoch,
			AuthorityKind.OwnerPredictedHostValidated,
			identity,
			location,
			0,
			ToKernelData(item));
		TryExecuteCommand(command, actor, out _, out _);
	}

	private void TryUpdateStateExternal(ulong actor, ulong itemId, CharacterItemMsg item)
	{
		var current = _kernel.FindItem(itemId);
		if (current is null)
		{
			return;
		}

		var command = new UpdateItemStateCommand(
			NextOperation(),
			new ActorId(actor),
			_runEpoch,
			AuthorityKind.OwnerPredictedHostValidated,
			itemId,
			ToKernelData(item),
			current.Value.Revision);
		TryExecuteCommand(command, actor, out _, out _);
	}

	private void TryDestroyExternal(ulong actor, ulong itemId, TerminalKind kind)
	{
		var current = _kernel.FindItem(itemId);
		if (current is null)
		{
			return;
		}

		var command = new DestroyItemCommand(
			NextOperation(),
			new ActorId(actor),
			_runEpoch,
			AuthorityKind.HostOnly,
			itemId,
			kind,
			current.Value.Revision);
		TryExecuteCommand(command, actor, out _, out _);
	}

	// ===== Shadow-compatible conveniences (still used by diagnostics/tests) =====

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

	/// <summary>
	/// Convert a kernel item state back to the wire/save-shaped projection. This
	/// does not fill contents; projections assemble contained children from the
	/// kernel separately.
	/// </summary>
	public static CharacterItemMsg ToCharacterItem(ItemState state) =>
		new()
		{
			InstanceId = state.Identity.InstanceId,
			ItemId = state.Identity.DefinitionId,
			Condition = state.Data.Condition,
			SlotIndex = state.Data.SlotIndex,
			Favourited = state.Data.Favourited,
			Liquids = [.. state.Data.Liquids.Select(l => new LiquidStackMsg { LiquidId = l.LiquidId, Amount = l.Amount })],
			Components = [.. state.Data.Components.Select(ToWireComponent)],
		};

	private static ComponentStateMsg ToWireComponent(ItemComponentState state) =>
		new()
		{
			TypeName = state.TypeName,
			Fields = [.. state.Fields.Select(f => new ComponentFieldMsg
			{
				Name = f.Name,
				Kind = (int)f.Kind,
				FloatValue = f.FloatValue,
				IntValue = f.IntValue,
				BoolValue = f.BoolValue,
				StringValue = f.StringValue,
				StringList = [.. f.StringList],
			})],
		};

	/// <summary>
	/// Convert a wire item message to the kernel-owned payload. Contents are not
	/// copied here: contained children are authoritative as separate ItemState
	/// entries and are reconciled by the projection layer.
	/// </summary>
	public static ItemData ToKernelData(CharacterItemMsg item) =>
		new(
			item.Condition,
			item.Favourited,
			item.SlotIndex,
			[.. item.Liquids.Select(l => new ItemLiquidStack(l.LiquidId, l.Amount))],
			[.. item.Components.Select(ToKernelComponent)]);

	private static ItemComponentState ToKernelComponent(ComponentStateMsg state) =>
		new(
			state.TypeName,
			[.. state.Fields.Select(f => new ItemComponentField(
				f.Name,
				(ItemComponentFieldKind)f.Kind,
				f.FloatValue,
				f.IntValue,
				f.BoolValue,
				f.StringValue,
				f.StringList))]);

	private OperationId NextOperation() => new(_nextOperation++);

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
