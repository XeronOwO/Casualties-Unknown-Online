namespace CasualtiesUnknownOnline.GameState.Domains.WorldEntities;

/// <summary>
/// One authoritative building-entity health record at a world-cell position.
/// </summary>
public sealed record BuildingEntityHealthFact(
	EntityPosition Position,
	float Health);
