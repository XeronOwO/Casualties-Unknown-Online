namespace CasualtiesUnknownOnline.GameState.Domains.Items;

/// <summary>
/// A carried item changes owner without leaving the carried location (a
/// cross-player transfer from one bag to another). This is not a pickup/drop:
/// the item never enters World.
/// </summary>
public sealed record TransferItemCommand(
	OperationId OperationId,
	ActorId Actor,
	RunEpoch RunEpoch,
	AuthorityKind Authority,
	ulong InstanceId,
	ActorId NewOwner,
	ItemData? NewData,
	ulong ExpectedRevision) : GameCommand(OperationId, Actor, RunEpoch, Authority, [new ExpectedRevision(InstanceId, ExpectedRevision)]);
