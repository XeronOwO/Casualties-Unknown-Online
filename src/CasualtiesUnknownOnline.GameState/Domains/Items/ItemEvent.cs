namespace CasualtiesUnknownOnline.GameState.Domains.Items;

/// <summary>
/// Base type for item-domain events.
/// </summary>
public abstract record ItemEvent(ItemIdentity Identity) : GameEvent;
