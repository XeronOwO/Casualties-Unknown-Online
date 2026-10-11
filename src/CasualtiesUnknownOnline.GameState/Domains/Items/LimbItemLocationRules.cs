using CasualtiesUnknownOnline.GameState.Kernel;

namespace CasualtiesUnknownOnline.GameState.Domains.Items;

/// <summary>
/// The rules for the ONE location family whose transitions this file owns:
/// APPLIED — an item that is the installed form of a limb component (a splint or
/// a tourniquet the game's own limb action turned into a component on a limb).
/// Extracted from <see cref="ItemDomainModule"/> (the 600-line architecture gate)
/// the way <see cref="ItemLocationChain"/> was, because the two decisions have
/// one responsibility the rest of the domain does not share: neither ever ends an
/// item.
///
/// <para>
/// Both directions are RELOCATIONS and both keep the instance id, which is the
/// ruling this family exists to implement: a splint is the same splint on the leg
/// and off it, so a mod that recorded the id, and CUO's own favourite-mark
/// ledger, can follow it across the transform. The alternative this family
/// deliberately is NOT: reusing the negative wear slot
/// (<c>ItemStateCodec.SlotOf</c>, <c>IWearSemantics</c>) would place the item on a
/// body AND leave it in the inventory, and the game has already destroyed the
/// carried object — an applied item is reached by no inventory gesture, no
/// search and no frisk.
/// </para>
///
/// <para>
/// What these rules do NOT decide: which limb may carry which component. The
/// game's own data gates that on the side that runs the action
/// (<c>Item.cs</c>'s <c>!limb.isVital</c> / <c>!limb.GetComponent&lt;SplintLimb&gt;()</c>
/// guards), a refused apply spends nothing, and no report is produced for it — so
/// the kernel only ever sees the outcome and must not invent a second gate that
/// would refuse a fact the scene already holds.
/// </para>
/// </summary>
internal static class LimbItemLocationRules
{
	/// <summary>
	/// The refusal every OTHER item command owes an item that is held by a limb. A
	/// pickup, a drop and a cook each move an item between inventory positions, and
	/// an applied item is in none of them: the item IS the limb component, so moving
	/// the row would leave the component behind — one item in two places. The
	/// family's own transitions have exactly two doors (<see cref="DecideApply"/>,
	/// <see cref="DecideRemove"/>); this is the sentence that keeps the rest shut,
	/// and it lives here so that everything the kernel says about the applied family
	/// reads in one place. A null return means "not applied", which is the common
	/// case and costs the caller one call.
	/// </summary>
	internal static DomainDecision? RefuseInventoryGesture(ulong instanceId, ItemLocation location, string gesture) =>
		location.Kind == ItemLocationKind.Applied
			? DomainDecision.Reject(
				RejectionReason.InvalidTransition,
				$"applied item {instanceId} cannot be {gesture}; it comes off the limb it is installed on")
			: null;

	/// <summary>
	/// The same sentence for the one command with no item row to read: a SPAWN may
	/// not create an item already installed on a limb. The family has one way in,
	/// and it is a relocation that needs the item to exist — and because only that
	/// limb's owner may report an applied row's end, a forged row would outlive
	/// every correction another peer could make.
	/// </summary>
	internal static DomainDecision? RefuseAppliedCreation(ItemLocation location) =>
		location.Kind == ItemLocationKind.Applied
			? DomainDecision.Reject(RejectionReason.InvalidTransition, "an item cannot spawn directly onto a limb")
			: null;

	/// <summary>
	/// Carried (or otherwise live) → applied. An item already applied stays
	/// applied and only advances its revision when the same fact is reported
	/// again — a repeated observation is a re-assertion, never a divergence.
	/// </summary>
	internal static DomainDecision DecideApply(ApplyLimbItemCommand command, KernelReadModel state)
	{
		var item = state.FindItem(command.InstanceId);
		if (item is null)
		{
			return DomainDecision.Reject(RejectionReason.UnknownAggregate, $"item {command.InstanceId} does not exist");
		}

		if (item.Value.Location.Kind == ItemLocationKind.Terminal)
		{
			return DomainDecision.Reject(RejectionReason.InvalidTransition, $"terminal item {command.InstanceId} cannot be applied to a limb");
		}

		if (command.Owner.Value == 0 || command.LimbIndex < 0)
		{
			return DomainDecision.Reject(RejectionReason.InvalidTransition,
				$"applying item {command.InstanceId} names no limb (owner {command.Owner.Value}, limb {command.LimbIndex})");
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
			ItemLocation.Applied(command.Owner, command.LimbIndex),
			command.Data ?? item.Value.Data));
	}

	/// <summary>
	/// Applied → carried by the operator (or by the limb's owner, for the
	/// amputation and dismember paths the native round trip itself takes). The
	/// limb the command names is checked against the item's own recorded location,
	/// so a removal can never land on a different limb than the one the gesture
	/// was made on.
	/// </summary>
	internal static DomainDecision DecideRemove(RemoveLimbItemCommand command, KernelReadModel state)
	{
		var item = state.FindItem(command.InstanceId);
		if (item is null)
		{
			return DomainDecision.Reject(RejectionReason.UnknownAggregate, $"item {command.InstanceId} does not exist");
		}

		if (item.Value.Location.Kind == ItemLocationKind.Terminal)
		{
			return DomainDecision.Reject(RejectionReason.InvalidTransition, $"terminal item {command.InstanceId} cannot be taken off a limb");
		}

		if (item.Value.Location.Kind != ItemLocationKind.Applied)
		{
			return DomainDecision.Reject(RejectionReason.InvalidTransition,
				$"removal requires an applied item, item {command.InstanceId} is {item.Value.Location.Kind}");
		}

		if (item.Value.Location.Owner != command.AppliedOwner || item.Value.Location.LimbIndex != command.LimbIndex)
		{
			return DomainDecision.Reject(RejectionReason.InvalidTransition,
				$"item {command.InstanceId} is applied to limb {item.Value.Location.LimbIndex} of {item.Value.Location.Owner.Value}, not limb {command.LimbIndex} of {command.AppliedOwner.Value}");
		}

		if (command.NewOwner.Value == 0)
		{
			return DomainDecision.Reject(RejectionReason.InvalidTransition, $"taking item {command.InstanceId} off a limb names no destination owner");
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
			command.Data ?? item.Value.Data));
	}
}
