namespace CasualtiesUnknownOnline.GameState.Domains.WorldEntities;

/// <summary>
/// A lockable entity was opened at a world position.
/// </summary>
public sealed record OpenedEntityEvent(
	EntityPosition Position) : WorldEntityEvent;
