namespace CasualtiesUnknownOnline.GameState.Domains.Items;

/// <summary>
/// An applied item comes off the limb it is installed on and is CARRIED by
/// <paramref name="NewOwner"/> — the game's own <c>TakeOff</c> path, expressed as
/// a move instead of a mint: the item keeps its instance id, so the mod that
/// recorded it follows it off the limb as well as onto it.
/// <para>
/// The removal names the limb it is the counterpart of, and
/// <paramref name="AppliedOwner"/> / <paramref name="LimbIndex"/> are checked
/// against the item's recorded location: the gesture carries the limb, so a
/// removal that names a different one is refused by name rather than executed
/// against whichever limb the id happens to sit on. The operator is usually not
/// the limb's owner (taking a splint off somebody else), which is why the
/// destination is its own operand.
/// </para>
/// </summary>
public sealed record RemoveLimbItemCommand(
	OperationId OperationId,
	ActorId Actor,
	RunEpoch RunEpoch,
	AuthorityKind Authority,
	ulong InstanceId,
	ActorId AppliedOwner,
	int LimbIndex,
	ActorId NewOwner,
	ulong ExpectedRevision,
	ItemData? Data = null) : GameCommand(OperationId, Actor, RunEpoch, Authority, [new ExpectedRevision(InstanceId, ExpectedRevision)]);
