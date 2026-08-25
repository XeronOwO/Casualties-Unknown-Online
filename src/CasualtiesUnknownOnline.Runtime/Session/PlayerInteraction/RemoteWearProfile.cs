namespace CasualtiesUnknownOnline.Runtime.Session.PlayerInteraction;

/// <summary>
/// The host-authoritative placement profile of one wearable item when it is
/// used on another player. Mirrors the native <c>ItemInfo</c> wearable fields
/// (<c>desiredWearLimb</c> / <c>wearSlotId</c>) so the Runtime can validate
/// and place the item without referencing the game assembly. Pure data — no
/// game assembly dependency, no state.
/// </summary>
public sealed record RemoteWearProfile(
	string ItemId,
	string WearSlotId,
	int LimbIndex);
