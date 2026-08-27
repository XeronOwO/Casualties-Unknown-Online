namespace CasualtiesUnknownOnline.GameState.Domains.Items;

/// <summary>
/// A new item enters the kernel. The expected revision for a new aggregate is 0.
/// The optional payload is the save-shaped item state carried by the creation
/// report; when omitted the item starts with empty data.
/// </summary>
public sealed record SpawnItemCommand(
	OperationId OperationId,
	ActorId Actor,
	RunEpoch RunEpoch,
	AuthorityKind Authority,
	ItemIdentity Identity,
	ItemLocation Location,
	ulong ExpectedRevision,
	ItemData? Data = null) : GameCommand(OperationId, Actor, RunEpoch, Authority, [new ExpectedRevision(Identity.InstanceId, ExpectedRevision)]);
