namespace CasualtiesUnknownOnline.GameState.Domains.Items;

/// <summary>
/// An item moves from World/Contained into the actor's carried inventory.
/// </summary>
public sealed record PickUpItemCommand(
	OperationId OperationId,
	ActorId Actor,
	RunEpoch RunEpoch,
	AuthorityKind Authority,
	ulong InstanceId,
	ActorId NewOwner,
	ulong ExpectedRevision) : GameCommand(OperationId, Actor, RunEpoch, Authority, [new ExpectedRevision(InstanceId, ExpectedRevision)]);
