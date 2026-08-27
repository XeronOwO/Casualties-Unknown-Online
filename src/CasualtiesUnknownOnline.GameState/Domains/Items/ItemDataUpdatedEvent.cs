namespace CasualtiesUnknownOnline.GameState.Domains.Items;

/// <summary>
/// An existing non-terminal item's save-shaped payload changed without a
/// location transition (use, slot move, liquid/component state update).
/// </summary>
public sealed record ItemDataUpdatedEvent(
	ItemIdentity Identity,
	ulong OldRevision,
	ulong NewRevision,
	ItemData OldData,
	ItemData NewData) : ItemEvent(Identity);
