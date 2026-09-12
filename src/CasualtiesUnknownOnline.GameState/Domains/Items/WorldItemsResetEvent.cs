namespace CasualtiesUnknownOnline.GameState.Domains.Items;

/// <summary>
/// Every world-rooted item was cleared for a new world/layer. The event carries
/// no payload: the reset is absolute — whatever the kernel held for the old
/// layer is gone, and the new layer's items enter as ordinary spawns.
/// </summary>
public sealed record WorldItemsResetEvent : ItemEvent;
