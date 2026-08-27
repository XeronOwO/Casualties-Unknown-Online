namespace CasualtiesUnknownOnline.GameState.Domains.Items;

/// <summary>
/// An existing item changed location and advanced its aggregate revision.
/// </summary>
public sealed record ItemRelocatedEvent(
	ItemIdentity Identity,
	ulong OldRevision,
	ulong NewRevision,
	ItemLocation OldLocation,
	ItemLocation NewLocation) : ItemEvent(Identity);
