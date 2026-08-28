namespace CasualtiesUnknownOnline.GameState.Domains.Items;

/// <summary>
/// A heater conversion: one source item is replaced by one cooked product in a
/// single atomic batch. The source reaches Terminal(ReplacedBy) while the
/// product is created in the same global revision.
/// </summary>
public sealed record CookItemCommand(
	OperationId OperationId,
	ActorId Actor,
	RunEpoch RunEpoch,
	AuthorityKind Authority,
	ItemIdentity SourceIdentity,
	ItemIdentity CookedIdentity,
	ItemLocation CookedLocation,
	ItemData? CookedData,
	ulong ExpectedSourceRevision) : GameCommand(
		OperationId,
		Actor,
		RunEpoch,
		Authority,
		[new ExpectedRevision(SourceIdentity.InstanceId, ExpectedSourceRevision)]);
