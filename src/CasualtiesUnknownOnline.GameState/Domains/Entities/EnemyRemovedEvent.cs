namespace CasualtiesUnknownOnline.GameState.Domains.Entities;

/// <summary>
/// An enemy/entity was removed from the kernel (destroyed/despawned).
/// </summary>
public sealed record EnemyRemovedEvent(EntityId EntityId) : EnemyEvent;
