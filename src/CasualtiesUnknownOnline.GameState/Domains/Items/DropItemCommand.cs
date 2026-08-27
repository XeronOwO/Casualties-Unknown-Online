namespace CasualtiesUnknownOnline.GameState.Domains.Items;

/// <summary>
/// A carried item leaves the actor's inventory and becomes a World or Contained
/// item at the supplied location.
/// </summary>
public sealed record DropItemCommand(
	OperationId OperationId,
	ActorId Actor,
	RunEpoch RunEpoch,
	AuthorityKind Authority,
	ulong InstanceId,
	ItemLocation NewLocation,
	ulong ExpectedRevision) : GameCommand(OperationId, Actor, RunEpoch, Authority, [new ExpectedRevision(InstanceId, ExpectedRevision)]);
