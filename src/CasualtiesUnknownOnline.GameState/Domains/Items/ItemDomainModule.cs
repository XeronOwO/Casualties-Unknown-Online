using System;
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
			or UpdateItemStateCommand or TransferItemCommand => true,
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

			var visited = new System.Collections.Generic.HashSet<ulong> { item.Identity.InstanceId };
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
