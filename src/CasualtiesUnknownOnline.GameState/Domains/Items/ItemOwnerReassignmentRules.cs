using CasualtiesUnknownOnline.GameState.Kernel;

namespace CasualtiesUnknownOnline.GameState.Domains.Items;

/// <summary>
/// The rules for re-keying an item row when the same player returns under a new
/// transport id. The owner key changes; the placement (slot, container parent,
/// limb) does not. World and terminal rows have no owner to re-key; a stale old
/// id is a different player's fact and is refused rather than silently
/// reassigned. Kept out of <see cref="ItemDomainModule"/> (the 600-line
/// architecture gate) the way <see cref="LimbItemLocationRules"/> is.
/// </summary>
internal static class ItemOwnerReassignmentRules
{
	internal static DomainDecision Decide(ReassignItemOwnerCommand command, KernelReadModel state)
	{
		var item = state.FindItem(command.InstanceId);
		if (item is null)
		{
			return DomainDecision.Reject(RejectionReason.UnknownAggregate, $"item {command.InstanceId} does not exist");
		}

		var location = item.Value.Location;
		if (location.Kind is ItemLocationKind.World or ItemLocationKind.Terminal)
		{
			return DomainDecision.Reject(RejectionReason.InvalidTransition,
				$"item {command.InstanceId} has no owner to reassign (current {location.Kind})");
		}

		if (location.Owner != command.OldOwner)
		{
			return DomainDecision.Reject(RejectionReason.InvalidTransition,
				$"item {command.InstanceId} is owned by {location.Owner.Value}, not the expected {command.OldOwner.Value}");
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
			location,
			location with { Owner = command.NewOwner },
			item.Value.Data));
	}
}
