namespace CasualtiesUnknownOnline.GameState.Domains.WorldEntities;

/// <summary>
/// One authoritative one-shot trap/mechanism consumption. <c>Kind</c> is an
/// opaque domain id mapped from the Runtime entity-event enum by the mapper;
/// <c>Extra</c> is kind-specific progress/slot data.
/// </summary>
public sealed record TrapConsumptionFact(
	EntityPosition Position,
	int Kind,
	byte Extra,
	long TriggeredAtMs);
