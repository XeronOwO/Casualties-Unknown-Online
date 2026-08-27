namespace CasualtiesUnknownOnline.GameState.Domains.Items;

/// <summary>
/// A new item enters the kernel. The expected revision for a new aggregate is 0.
/// </summary>
public sealed record SpawnItemCommand(
	OperationId OperationId,
	ActorId Actor,
	RunEpoch RunEpoch,
	AuthorityKind Authority,
	ItemIdentity Identity,
	ItemLocation Location,
	ulong ExpectedRevision) : GameCommand(OperationId, Actor, RunEpoch, Authority, [new ExpectedRevision(Identity.InstanceId, ExpectedRevision)]);
