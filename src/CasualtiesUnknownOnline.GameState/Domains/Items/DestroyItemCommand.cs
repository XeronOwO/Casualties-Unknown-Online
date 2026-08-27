namespace CasualtiesUnknownOnline.GameState.Domains.Items;

/// <summary>
/// An item is consumed/destroyed/replaced and moves to the Terminal location.
/// Terminal items cannot be picked up, dropped, or spawned again.
/// </summary>
public sealed record DestroyItemCommand(
	OperationId OperationId,
	ActorId Actor,
	RunEpoch RunEpoch,
	AuthorityKind Authority,
	ulong InstanceId,
	TerminalKind TerminalKind,
	ulong ExpectedRevision) : GameCommand(OperationId, Actor, RunEpoch, Authority, [new ExpectedRevision(InstanceId, ExpectedRevision)]);
