namespace CasualtiesUnknownOnline.GameState.Domains.Items;

/// <summary>
/// Base type for item-domain events. An event that names the item it changed
/// derives from <see cref="ItemTargetedEvent"/>; a table-wide event
/// (<see cref="WorldItemsResetEvent"/>) derives from this type directly, because
/// it has no single item to name.
/// </summary>
public abstract record ItemEvent : GameEvent;
