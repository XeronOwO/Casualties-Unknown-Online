namespace CasualtiesUnknownOnline.GameState.Domains.Players;

/// <summary>
/// Host-only command that records one cross-player inventory transfer result in
/// the kernel journal. The durable item ownership/state mutation already rides
/// the item domain; this command carries the result payload that lets every
/// participant's projection apply the authoritative local body mutation.
/// </summary>
public sealed record RecordPlayerInventoryTransferCommand(
	OperationId OperationId,
	ActorId Actor,
	RunEpoch RunEpoch,
	AuthorityKind Authority,
	ulong FromSteamId,
	ulong ToSteamId,
	PlayerInteractionItem Item,
	ulong TargetParentItemId = 0) : GameCommand(OperationId, Actor, RunEpoch, Authority, []);
