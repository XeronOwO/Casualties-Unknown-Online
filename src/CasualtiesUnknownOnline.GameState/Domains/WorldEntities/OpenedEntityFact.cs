namespace CasualtiesUnknownOnline.GameState.Domains.WorldEntities;

/// <summary>
/// One authoritative opened lockable-entity fact at a world-cell position.
/// </summary>
public sealed record OpenedEntityFact(
	EntityPosition Position);
