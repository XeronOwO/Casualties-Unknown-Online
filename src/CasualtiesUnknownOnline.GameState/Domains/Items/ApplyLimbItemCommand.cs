namespace CasualtiesUnknownOnline.GameState.Domains.Items;

/// <summary>
/// An item becomes the installed form of a limb component of
/// <paramref name="Owner"/>'s limb <paramref name="LimbIndex"/> — the game's own
/// limb tool turned it into a <c>SplintLimb</c> / <c>TourniquetScript</c> on that
/// limb. The item keeps its instance id and its revision advances: applying is a
/// relocation, never an end.
/// <para>
/// The optional payload carries the save-shaped state the reporter knows at the
/// same moment (the item's condition as the component took it); when omitted the
/// kernel keeps the existing data.
/// </para>
/// </summary>
public sealed record ApplyLimbItemCommand(
	OperationId OperationId,
	ActorId Actor,
	RunEpoch RunEpoch,
	AuthorityKind Authority,
	ulong InstanceId,
	ActorId Owner,
	int LimbIndex,
	ulong ExpectedRevision,
	ItemData? Data = null) : GameCommand(OperationId, Actor, RunEpoch, Authority, [new ExpectedRevision(InstanceId, ExpectedRevision)]);
