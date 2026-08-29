namespace CasualtiesUnknownOnline.GameState.Domains.Players;

/// <summary>
/// A player's terminal status (alive/conscious) was updated in the kernel.
/// </summary>
public sealed record PlayerStatusUpdatedEvent(PlayerState State) : PlayerEvent;
