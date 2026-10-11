using System;
using CasualtiesUnknownOnline.GameState;
using CasualtiesUnknownOnline.GameState.Domains.Items;
using CasualtiesUnknownOnline.Protocol.Wire;

namespace CasualtiesUnknownOnline.Application.Kernel;

/// <summary>
/// The wire mapping of the two LIMB-ITEM commands — the applied family's member half.
/// Split out of <see cref="KernelWireMapper"/> at the architecture line gate, and kept
/// separate for a better reason than size: these two are the only commands whose
/// decisive operand is the applied LOCATION rather than a scalar, so their operand
/// parsing belongs in one place a reader can check without reading the whole mapper.
/// </summary>
internal static class KernelLimbItemWireMapper
{
	/// <summary>
	/// A member reports that its own carried item became the installed form of one of
	/// its limbs: the wire names that limb in the applied location, which is the same
	/// carrier a committed applied row uses.
	/// </summary>
	internal static GameCommand FromWireApplyLimbCommand(WireCommand command, OperationId operation, ActorId actor, RunEpoch epoch, AuthorityKind authority, ulong instanceId)
	{
		var (owner, limbIndex) = AppliedLimbOperand(command);
		return new ApplyLimbItemCommand(
			operation,
			actor,
			epoch,
			authority,
			instanceId,
			owner,
			limbIndex,
			command.ExpectedRevision,
			command.Data is null ? null : KernelWireMapper.FromWireData(command.Data));
	}

	/// <summary>
	/// The counterpart: the limb's component came off as this item, which is now
	/// CARRIED by the wire's <c>NewOwner</c> — its own operand, because the operator
	/// taking a splint off somebody else is not the limb's owner.
	/// </summary>
	internal static GameCommand FromWireRemoveLimbCommand(WireCommand command, OperationId operation, ActorId actor, RunEpoch epoch, AuthorityKind authority, ulong instanceId)
	{
		var (appliedOwner, limbIndex) = AppliedLimbOperand(command);
		return new RemoveLimbItemCommand(
			operation,
			actor,
			epoch,
			authority,
			instanceId,
			appliedOwner,
			limbIndex,
			new ActorId(command.NewOwner),
			command.ExpectedRevision,
			command.Data is null ? null : KernelWireMapper.FromWireData(command.Data));
	}

	/// <summary>
	/// The applied-location operand both limb commands carry, parsed through the SAME
	/// location mapper a committed row uses — so a command that names no location, names
	/// another family, or names an applied location without a limb is refused here exactly
	/// as it is on the batch path. Reading an absent limb as 0 would install the item on
	/// the head.
	/// </summary>
	private static (ActorId Owner, int LimbIndex) AppliedLimbOperand(WireCommand command)
	{
		var location = command.Location
			?? throw new InvalidOperationException($"{command.Kind} command lacks the applied limb it is about");
		var parsed = KernelWireMapper.FromWireLocation(location);
		if (parsed.Kind != ItemLocationKind.Applied)
		{
			throw new InvalidOperationException($"{command.Kind} command names a {parsed.Kind} location instead of an applied one");
		}

		return (parsed.Owner, parsed.LimbIndex);
	}
}
