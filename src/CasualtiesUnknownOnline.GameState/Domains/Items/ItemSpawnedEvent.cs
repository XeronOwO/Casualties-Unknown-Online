namespace CasualtiesUnknownOnline.GameState.Domains.Items;

/// <summary>
/// An item was created in the kernel with revision 1. The optional payload is
/// the save-shaped item state from the creation batch.
/// </summary>
public sealed record ItemSpawnedEvent(
	ItemIdentity Identity,
	ulong Revision,
	ItemLocation Location,
	ItemData? Data = null) : ItemEvent(Identity);
