namespace CasualtiesUnknownOnline.GameState.Domains.Items;

/// <summary>
/// The same player reconnected under a NEW transport id: move the item rows the
/// kernel keys by the old id (carried, contained, applied) to the new id while
/// preserving each row's placement — the slot, the container parent, the limb.
/// This is not a gameplay transfer: no item changes hands, only the session
/// identity the ownership fact is keyed by. World and terminal rows have no
/// owner to re-key and are refused.
/// </summary>
public sealed record ReassignItemOwnerCommand(
	OperationId OperationId,
	ActorId Actor,
	RunEpoch RunEpoch,
	AuthorityKind Authority,
	ulong InstanceId,
	ActorId OldOwner,
	ActorId NewOwner,
	ulong ExpectedRevision) : GameCommand(OperationId, Actor, RunEpoch, Authority, [new ExpectedRevision(InstanceId, ExpectedRevision)]);
