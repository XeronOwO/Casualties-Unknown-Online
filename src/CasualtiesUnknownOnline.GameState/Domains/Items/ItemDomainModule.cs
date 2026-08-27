using System;
using CasualtiesUnknownOnline.GameState.Kernel;

namespace CasualtiesUnknownOnline.GameState.Domains.Items;

/// <summary>
/// Phase A item slice: Spawn, PickUp, Drop, Destroy with unique location,
/// no Terminal resurrection, wrong-revision rejection, and deterministic
/// reduction.
/// </summary>
internal sealed class ItemDomainModule : IDomainModule
{
	public bool CanHandle(GameCommand command) => command switch
	{
		SpawnItemCommand or PickUpItemCommand or DropItemCommand or DestroyItemCommand => true,
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
			_ => DomainDecision.Reject(RejectionReason.UnknownCommand, $"unknown item command {command.GetType().Name}"),
		};

	public void Reduce(GameEvent @event, MutableKernelState state)
	{
		switch (@event)
		{
			case ItemSpawnedEvent spawned:
				state.UpsertItem(new ItemState(spawned.Identity, spawned.Revision, spawned.Location));
				break;
			case ItemRelocatedEvent relocated:
				state.UpsertItem(new ItemState(relocated.Identity, relocated.NewRevision, relocated.NewLocation));
				break;
			case ItemDestroyedEvent destroyed:
				state.UpsertItem(new ItemState(destroyed.Identity, destroyed.Revision, destroyed.TerminalLocation));
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
			}
		}
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

		return DomainDecision.Accept(new ItemSpawnedEvent(command.Identity, 1, command.Location));
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

		if (item.Value.Location.Kind is not (ItemLocationKind.Carried or ItemLocationKind.World or ItemLocationKind.Contained))
		{
			return DomainDecision.Reject(RejectionReason.InvalidTransition, $"item {command.InstanceId} cannot be dropped from {item.Value.Location.Kind}");
		}

		if (item.Value.Location.Kind == ItemLocationKind.Carried && item.Value.Location.Owner != command.Actor)
		{
			return DomainDecision.Reject(RejectionReason.NotAuthorized, $"item {command.InstanceId} is not owned by the dropping actor");
		}

		if (command.NewLocation.Kind is not (ItemLocationKind.World or ItemLocationKind.Contained))
		{
			return DomainDecision.Reject(RejectionReason.InvalidTransition, $"drop target must be World or Contained");
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
			command.NewLocation));
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
}
