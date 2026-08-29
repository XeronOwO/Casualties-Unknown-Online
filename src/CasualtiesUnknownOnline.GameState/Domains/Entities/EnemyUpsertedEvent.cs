namespace CasualtiesUnknownOnline.GameState.Domains.Entities;

/// <summary>
/// An enemy/entity fact was upserted in the kernel.
/// </summary>
public sealed record EnemyUpsertedEvent(EnemyState State) : EnemyEvent;
