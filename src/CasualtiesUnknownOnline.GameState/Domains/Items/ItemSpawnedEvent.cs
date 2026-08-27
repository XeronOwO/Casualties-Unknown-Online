namespace CasualtiesUnknownOnline.GameState.Domains.Items;

/// <summary>
/// An item was created in the kernel with revision 1.
/// </summary>
public sealed record ItemSpawnedEvent(ItemIdentity Identity, ulong Revision, ItemLocation Location) : ItemEvent(Identity);
