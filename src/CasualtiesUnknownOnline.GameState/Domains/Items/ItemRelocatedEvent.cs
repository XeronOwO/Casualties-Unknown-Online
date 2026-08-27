namespace CasualtiesUnknownOnline.GameState.Domains.Items;

/// <summary>
/// An existing item changed location and advanced its aggregate revision. The
/// optional payload carries a simultaneous saved-data change (drop, transfer);
/// when omitted the reducer preserves the existing data.
/// </summary>
public sealed record ItemRelocatedEvent(
	ItemIdentity Identity,
	ulong OldRevision,
	ulong NewRevision,
	ItemLocation OldLocation,
	ItemLocation NewLocation,
	ItemData? NewData = null) : ItemEvent(Identity);
