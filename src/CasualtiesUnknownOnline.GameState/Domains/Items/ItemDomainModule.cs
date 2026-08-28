using System;
using System.Collections.Generic;
using System.Linq;
using CasualtiesUnknownOnline.GameState.Kernel;

namespace CasualtiesUnknownOnline.GameState.Domains.Items;

/// <summary>
/// Items domain module. Phase A covered Spawn/PickUp/Drop/Destroy with unique
/// location and no Terminal resurrection; Phase B adds the saved-data payload,
/// state-update commands, carried transfers, and container-parent invariants.
/// Reducers are deterministic and never touch ambient state.
/// </summary>
internal sealed class ItemDomainModule : IDomainModule
{
	public bool CanHandle(GameCommand command) => command switch
	{
		SpawnItemCommand or PickUpItemCommand or DropItemCommand or DestroyItemCommand
			or UpdateItemStateCommand or TransferItemCommand or CookItemCommand
			or SyncContainerItemsCommand => true,
		_ => false,
	};

	public bool CanReduce(GameEvent @event) => @event is ItemEvent;

	public DomainDecision Decide(GameCommand command, KernelReadModel state, CommandContext context) =>
		command switch
		{
			SpawnItemCommand spawn => DecideSpawn(spawn, state),
			PickUpItemCommand pickup => DecidePickup(pickup, state),
			DropItemCommand drop => DecideDrop(drop, state),
			DestroyItemCommand destroy => DecideDestroy(destroy, state),
			UpdateItemStateCommand update => DecideUpdate(update, state),
			TransferItemCommand transfer => DecideTransfer(transfer, state),
			CookItemCommand cook => DecideCook(cook, state),
			SyncContainerItemsCommand sync => DecideSyncContainer(sync, state),
			_ => DomainDecision.Reject(RejectionReason.UnknownCommand, $"unknown item command {command.GetType().Name}"),
		};

	public void Reduce(GameEvent @event, MutableKernelState state)
	{
		switch (@event)
		{
			case ItemSpawnedEvent spawned:
				state.UpsertItem(new ItemState(spawned.Identity, spawned.Revision, spawned.Location)
				{
					Data = spawned.Data ?? ItemData.Empty
				});
				break;
			case ItemRelocatedEvent relocated:
				state.UpsertItem(BuildRelocated(relocated, state));
				break;
			case ItemDestroyedEvent destroyed:
				state.UpsertItem(BuildDestroyed(destroyed, state));
				break;
			case ItemDataUpdatedEvent updated:
				state.UpsertItem(new ItemState(updated.Identity, updated.NewRevision, CurrentLocation(state, updated.Identity.InstanceId))
				{
					Data = updated.NewData
				});
				break;
			default:
				throw new InvalidOperationException($"unknown item event {@event.GetType().Name}");
		}
	}

	public void AssertInvariants(KernelReadModel state)
	{
		foreach (var item in state.Items.Values)
		{
			if (item.Revision == 0)
			{
				throw new InvalidOperationException($"item {item.Identity.InstanceId} has revision 0");
			}

			switch (item.Location.Kind)
			{
				case ItemLocationKind.Carried when item.Location.Owner.Value == 0:
					throw new InvalidOperationException($"carried item {item.Identity.InstanceId} has no owner");
				case ItemLocationKind.Contained when item.Location.ParentItemId == 0:
					throw new InvalidOperationException($"contained item {item.Identity.InstanceId} has no parent");
				case ItemLocationKind.Contained:
					AssertContainedParentExists(state, item);
					break;
			}
		}

		AssertNoContainerCycles(state);
	}

	private static DomainDecision DecideSpawn(SpawnItemCommand command, KernelReadModel state)
	{
		if (command.Location.Kind == ItemLocationKind.Terminal)
		{
			return DomainDecision.Reject(RejectionReason.InvalidTransition, "an item cannot spawn directly into Terminal");
		}

		if (state.FindItem(command.Identity.InstanceId) is not null)
		{
			return DomainDecision.Reject(RejectionReason.Conflict, $"item {command.Identity.InstanceId} already exists");
		}

		if (command.ExpectedRevision != 0)
		{
			return DomainDecision.Reject(RejectionReason.WrongRevision, $"new item {command.Identity.InstanceId} expects revision {command.ExpectedRevision}, not 0");
		}

		if (command.Location.Kind == ItemLocationKind.Contained)
		{
			var parent = state.FindItem(command.Location.ParentItemId);
			if (parent is null)
			{
				return DomainDecision.Reject(RejectionReason.UnknownAggregate, $"container {command.Location.ParentItemId} does not exist");
			}

			if (parent.Value.Location.Kind == ItemLocationKind.Terminal)
			{
				return DomainDecision.Reject(RejectionReason.InvalidTransition, $"terminal container {command.Location.ParentItemId} cannot accept children");
			}
		}

		return DomainDecision.Accept(new ItemSpawnedEvent(command.Identity, 1, command.Location, command.Data ?? ItemData.Empty));
	}

	private static DomainDecision DecidePickup(PickUpItemCommand command, KernelReadModel state)
	{
		var item = state.FindItem(command.InstanceId);
		if (item is null)
		{
			return DomainDecision.Reject(RejectionReason.UnknownAggregate, $"item {command.InstanceId} does not exist");
		}

		if (item.Value.Location.Kind == ItemLocationKind.Terminal)
		{
			return DomainDecision.Reject(RejectionReason.InvalidTransition, $"terminal item {command.InstanceId} cannot be picked up");
		}

		if (item.Value.Location.Kind == ItemLocationKind.Carried)
		{
			return DomainDecision.Reject(RejectionReason.Conflict, $"item {command.InstanceId} is already carried");
		}

		if (item.Value.Revision != command.ExpectedRevision)
		{
			return DomainDecision.Reject(RejectionReason.WrongRevision,
				$"item {command.InstanceId} revision {item.Value.Revision} does not match expected {command.ExpectedRevision}");
		}

		return DomainDecision.Accept(new ItemRelocatedEvent(
			item.Value.Identity,
			item.Value.Revision,
			item.Value.Revision + 1,
			item.Value.Location,
			ItemLocation.Carried(command.NewOwner)));
	}

	private static DomainDecision DecideDrop(DropItemCommand command, KernelReadModel state)
	{
		var item = state.FindItem(command.InstanceId);
		if (item is null)
		{
			return DomainDecision.Reject(RejectionReason.UnknownAggregate, $"item {command.InstanceId} does not exist");
		}

		if (item.Value.Location.Kind == ItemLocationKind.Terminal)
		{
			return DomainDecision.Reject(RejectionReason.InvalidTransition, $"terminal item {command.InstanceId} cannot be dropped");
		}

		if (item.Value.Location.Kind == ItemLocationKind.Carried && item.Value.Location.Owner != command.Actor)
		{
			return DomainDecision.Reject(RejectionReason.NotAuthorized, $"item {command.InstanceId} is not owned by the dropping actor");
		}

		if (command.NewLocation.Kind is not (ItemLocationKind.World or ItemLocationKind.Contained))
		{
			return DomainDecision.Reject(RejectionReason.InvalidTransition, $"drop target must be World or Contained");
		}

		if (command.NewLocation.Kind == ItemLocationKind.Contained)
		{
			var parent = state.FindItem(command.NewLocation.ParentItemId);
			if (parent is null)
			{
				return DomainDecision.Reject(RejectionReason.UnknownAggregate, $"container {command.NewLocation.ParentItemId} does not exist");
			}

			if (parent.Value.Location.Kind == ItemLocationKind.Terminal)
			{
				return DomainDecision.Reject(RejectionReason.InvalidTransition, $"terminal container {command.NewLocation.ParentItemId} cannot accept children");
			}
		}

		if (item.Value.Revision != command.ExpectedRevision)
		{
			return DomainDecision.Reject(RejectionReason.WrongRevision,
				$"item {command.InstanceId} revision {item.Value.Revision} does not match expected {command.ExpectedRevision}");
		}

		return DomainDecision.Accept(new ItemRelocatedEvent(
			item.Value.Identity,
			item.Value.Revision,
			item.Value.Revision + 1,
			item.Value.Location,
			command.NewLocation,
			command.Data ?? item.Value.Data));
	}

	private static DomainDecision DecideDestroy(DestroyItemCommand command, KernelReadModel state)
	{
		var item = state.FindItem(command.InstanceId);
		if (item is null)
		{
			return DomainDecision.Reject(RejectionReason.UnknownAggregate, $"item {command.InstanceId} does not exist");
		}

		if (item.Value.Location.Kind == ItemLocationKind.Terminal)
		{
			return DomainDecision.Reject(RejectionReason.InvalidTransition, $"terminal item {command.InstanceId} cannot be destroyed again");
		}

		if (item.Value.Revision != command.ExpectedRevision)
		{
			return DomainDecision.Reject(RejectionReason.WrongRevision,
				$"item {command.InstanceId} revision {item.Value.Revision} does not match expected {command.ExpectedRevision}");
		}

		return DomainDecision.Accept(new ItemDestroyedEvent(
			item.Value.Identity,
			item.Value.Revision + 1,
			ItemLocation.Terminal(),
			command.TerminalKind));
	}

	private static DomainDecision DecideUpdate(UpdateItemStateCommand command, KernelReadModel state)
	{
		var item = state.FindItem(command.InstanceId);
		if (item is null)
		{
			return DomainDecision.Reject(RejectionReason.UnknownAggregate, $"item {command.InstanceId} does not exist");
		}

		if (item.Value.Location.Kind == ItemLocationKind.Terminal)
		{
			return DomainDecision.Reject(RejectionReason.InvalidTransition, $"terminal item {command.InstanceId} cannot update payload");
		}

		if (item.Value.Revision != command.ExpectedRevision)
		{
			return DomainDecision.Reject(RejectionReason.WrongRevision,
				$"item {command.InstanceId} revision {item.Value.Revision} does not match expected {command.ExpectedRevision}");
		}

		return DomainDecision.Accept(new ItemDataUpdatedEvent(
			item.Value.Identity,
			item.Value.Revision,
			item.Value.Revision + 1,
			item.Value.Data,
			command.NewData));
	}

	private static DomainDecision DecideTransfer(TransferItemCommand command, KernelReadModel state)
	{
		var item = state.FindItem(command.InstanceId);
		if (item is null)
		{
			return DomainDecision.Reject(RejectionReason.UnknownAggregate, $"item {command.InstanceId} does not exist");
		}

		if (item.Value.Location.Kind != ItemLocationKind.Carried)
		{
			return DomainDecision.Reject(RejectionReason.InvalidTransition, $"transfer requires a carried item, current {item.Value.Location.Kind}");
		}

		if (item.Value.Revision != command.ExpectedRevision)
		{
			return DomainDecision.Reject(RejectionReason.WrongRevision,
				$"item {command.InstanceId} revision {item.Value.Revision} does not match expected {command.ExpectedRevision}");
		}

		return DomainDecision.Accept(new ItemRelocatedEvent(
			item.Value.Identity,
			item.Value.Revision,
			item.Value.Revision + 1,
			item.Value.Location,
			ItemLocation.Carried(command.NewOwner),
			command.NewData ?? item.Value.Data));
	}

	private static DomainDecision DecideSyncContainer(SyncContainerItemsCommand command, KernelReadModel state)
	{
		var parentId = command.ParentIdentity.InstanceId;
		var parent = state.FindItem(parentId);
		var events = new List<GameEvent>();
		if (parent is null)
		{
			events.Add(new ItemSpawnedEvent(
				command.ParentIdentity,
				1,
				ItemLocation.Carried(command.Actor),
				command.ParentData));
		}
		else
		{
			if (parent.Value.Location.Kind == ItemLocationKind.Terminal)
			{
				return DomainDecision.Reject(RejectionReason.InvalidTransition,
					$"terminal container {parentId} cannot accept children");
			}

			if (!parent.Value.Data.SemanticallyEquals(command.ParentData))
			{
				events.Add(new ItemDataUpdatedEvent(
					parent.Value.Identity,
					parent.Value.Revision,
					parent.Value.Revision + 1,
					parent.Value.Data,
					command.ParentData));
			}
		}

		var desired = new HashSet<ulong>();
		foreach (var child in command.Children)
		{
			if (child.InstanceId == 0 || !desired.Add(child.InstanceId))
			{
				continue;
			}

			var current = state.FindItem(child.InstanceId);
			if (current is null)
			{
				events.Add(new ItemSpawnedEvent(
					new ItemIdentity(child.InstanceId, child.DefinitionId),
					1,
					ItemLocation.Contained(command.Actor, child.ParentItemId),
					child.Data));
				continue;
			}

			if (current.Value.Location.Kind == ItemLocationKind.Terminal)
			{
				return DomainDecision.Reject(RejectionReason.InvalidTransition,
					$"terminal child {child.InstanceId} cannot re-enter container {parentId}");
			}

			var sameParent = current.Value.Location.Kind == ItemLocationKind.Contained
				&& current.Value.Location.ParentItemId == child.ParentItemId;
			if (sameParent && current.Value.Data.SemanticallyEquals(child.Data))
			{
				continue;
			}

			if (sameParent)
			{
				events.Add(new ItemDataUpdatedEvent(
					current.Value.Identity,
					current.Value.Revision,
					current.Value.Revision + 1,
					current.Value.Data,
					child.Data));
			}
			else
			{
				events.Add(new ItemRelocatedEvent(
					current.Value.Identity,
					current.Value.Revision,
					current.Value.Revision + 1,
					current.Value.Location,
					ItemLocation.Contained(command.Actor, child.ParentItemId),
					child.Data));
			}
		}

		var stale = state.Items.Values
			.Where(i =>
				i.Location.Kind == ItemLocationKind.Contained
				&& !desired.Contains(i.Identity.InstanceId)
				&& IsDescendantOf(i.Identity.InstanceId, parentId, state))
			.Select(i => (Id: i.Identity.InstanceId, Depth: ContainedDepth(i.Identity.InstanceId, parentId, state)))
			.OrderByDescending(x => x.Depth)
			.ToList();

		foreach (var (id, _) in stale)
		{
			var current = state.FindItem(id);
			if (current is null)
			{
				continue;
			}

			events.Add(new ItemDestroyedEvent(
				current.Value.Identity,
				current.Value.Revision + 1,
				ItemLocation.Terminal(),
				TerminalKind.ReplacedBy));
		}

		return DomainDecision.Accept([.. events]);
	}

	private static bool IsDescendantOf(ulong itemId, ulong ancestorId, KernelReadModel state)
	{
		var current = state.FindItem(itemId);
		if (current is null || current.Value.Location.Kind != ItemLocationKind.Contained)
		{
			return false;
		}

		var visited = new HashSet<ulong>();
		var cursor = current.Value.Location.ParentItemId;
		while (cursor != 0 && visited.Add(cursor))
		{
			if (cursor == ancestorId)
			{
				return true;
			}

			var parent = state.FindItem(cursor);
			if (parent is null || parent.Value.Location.Kind != ItemLocationKind.Contained)
			{
				return false;
			}

			cursor = parent.Value.Location.ParentItemId;
		}

		return false;
	}

	private static int ContainedDepth(ulong itemId, ulong ancestorId, KernelReadModel state)
	{
		var current = state.FindItem(itemId);
		if (current is null || current.Value.Location.Kind != ItemLocationKind.Contained)
		{
			return -1;
		}

		var depth = 0;
		var visited = new HashSet<ulong>();
		var cursor = current.Value.Location.ParentItemId;
		while (cursor != 0 && visited.Add(cursor))
		{
			depth++;
			if (cursor == ancestorId)
			{
				return depth;
			}

			var parent = state.FindItem(cursor);
			if (parent is null || parent.Value.Location.Kind != ItemLocationKind.Contained)
			{
				return -1;
			}

			cursor = parent.Value.Location.ParentItemId;
		}

		return -1;
	}

	private static DomainDecision DecideCook(CookItemCommand command, KernelReadModel state)
	{
		if (state.FindItem(command.CookedIdentity.InstanceId) is not null)
		{
			return DomainDecision.Reject(RejectionReason.Conflict,
				$"cooked item {command.CookedIdentity.InstanceId} already exists");
		}

		var source = state.FindItem(command.SourceIdentity.InstanceId);
		if (source is null || source.Value.Location.Kind == ItemLocationKind.Terminal)
		{
			// Accept-first: a native cooker observation may precede the source
			// entering the kernel. The product is still committed; a source that
			// already reached Terminal has no further destroy to record.
			return DomainDecision.Accept(
				new ItemSpawnedEvent(
					command.CookedIdentity,
					1,
					command.CookedLocation,
					command.CookedData ?? ItemData.Empty));
		}

		if (source.Value.Revision != command.ExpectedSourceRevision)
		{
			return DomainDecision.Reject(RejectionReason.WrongRevision,
				$"source item {command.SourceIdentity.InstanceId} revision {source.Value.Revision} does not match expected {command.ExpectedSourceRevision}");
		}

		return DomainDecision.Accept(
			new ItemDestroyedEvent(
				source.Value.Identity,
				source.Value.Revision + 1,
				ItemLocation.Terminal(),
				TerminalKind.ReplacedBy),
			new ItemSpawnedEvent(
				command.CookedIdentity,
				1,
				command.CookedLocation,
				command.CookedData ?? ItemData.Empty));
	}

	private static ItemState BuildRelocated(ItemRelocatedEvent relocated, MutableKernelState state)
	{
		var current = state.TryGetItem(relocated.Identity.InstanceId, out var existing)
			? existing
			: new ItemState(relocated.Identity, relocated.OldRevision, relocated.OldLocation);
		return new ItemState(relocated.Identity, relocated.NewRevision, relocated.NewLocation)
		{
			Data = relocated.NewData ?? current.Data
		};
	}

	private static ItemState BuildDestroyed(ItemDestroyedEvent destroyed, MutableKernelState state)
	{
		var data = state.TryGetItem(destroyed.Identity.InstanceId, out var existing)
			? existing.Data
			: ItemData.Empty;
		return new ItemState(destroyed.Identity, destroyed.Revision, destroyed.TerminalLocation)
		{
			Data = data
		};
	}

	private static ItemLocation CurrentLocation(MutableKernelState state, ulong instanceId) =>
		state.TryGetItem(instanceId, out var item) ? item.Location : ItemLocation.Terminal();

	private static void AssertContainedParentExists(KernelReadModel state, ItemState item)
	{
		var parent = state.FindItem(item.Location.ParentItemId);
		if (parent is null)
		{
			throw new InvalidOperationException($"contained item {item.Identity.InstanceId} has missing parent {item.Location.ParentItemId}");
		}

		if (parent.Value.Location.Kind == ItemLocationKind.Terminal)
		{
			throw new InvalidOperationException($"contained item {item.Identity.InstanceId} has a terminal parent");
		}

		if (parent.Value.Identity.InstanceId == item.Identity.InstanceId)
		{
			throw new InvalidOperationException($"item {item.Identity.InstanceId} is its own parent");
		}
	}

	private static void AssertNoContainerCycles(KernelReadModel state)
	{
		foreach (var item in state.Items.Values)
		{
			if (item.Location.Kind != ItemLocationKind.Contained)
			{
				continue;
			}

			var visited = new HashSet<ulong> { item.Identity.InstanceId };
			var cursor = item.Location.ParentItemId;
			while (cursor != 0)
			{
				if (!visited.Add(cursor))
				{
					throw new InvalidOperationException($"container cycle detected at item {item.Identity.InstanceId} / parent {cursor}");
				}

				var parent = state.FindItem(cursor);
				if (parent is null || parent.Value.Location.Kind != ItemLocationKind.Contained)
				{
					break;
				}

				cursor = parent.Value.Location.ParentItemId;
			}
		}
	}
}
