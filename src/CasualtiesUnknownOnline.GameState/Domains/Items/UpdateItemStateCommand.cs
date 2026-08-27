namespace CasualtiesUnknownOnline.GameState.Domains.Items;

/// <summary>
/// An existing non-terminal item's save-shaped payload changed without a
/// location transition (use, slot move, liquid/component drift, container
/// content replacement on the parent). The command advances the aggregate
/// revision because the item fact changed.
/// </summary>
public sealed record UpdateItemStateCommand(
	OperationId OperationId,
	ActorId Actor,
	RunEpoch RunEpoch,
	AuthorityKind Authority,
	ulong InstanceId,
	ItemData NewData,
	ulong ExpectedRevision) : GameCommand(OperationId, Actor, RunEpoch, Authority, [new ExpectedRevision(InstanceId, ExpectedRevision)]);
