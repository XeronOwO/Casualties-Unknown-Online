namespace CasualtiesUnknownOnline.GameState.Domains.Items;

/// <summary>
/// A carried item leaves the actor's inventory and becomes a World or Contained
/// item at the supplied location. The optional payload carries the save-shaped
/// state from the drop report; when omitted the kernel keeps the existing data.
/// </summary>
public sealed record DropItemCommand(
	OperationId OperationId,
	ActorId Actor,
	RunEpoch RunEpoch,
	AuthorityKind Authority,
	ulong InstanceId,
	ItemLocation NewLocation,
	ulong ExpectedRevision,
	ItemData? Data = null) : GameCommand(OperationId, Actor, RunEpoch, Authority, [new ExpectedRevision(InstanceId, ExpectedRevision)]);
