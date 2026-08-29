namespace CasualtiesUnknownOnline.GameState.Domains.WorldEntities;

/// <summary>
/// A building entity's current health was updated at a world position.
/// </summary>
public sealed record BuildingEntityHealthUpdatedEvent(
	EntityPosition Position,
	float Health) : WorldEntityEvent;
