namespace CasualtiesUnknownOnline.GameState.Domains.Items;

/// <summary>
/// Base type for an item-domain event that names the item it changed.
/// </summary>
public abstract record ItemTargetedEvent(ItemIdentity Identity) : ItemEvent;
