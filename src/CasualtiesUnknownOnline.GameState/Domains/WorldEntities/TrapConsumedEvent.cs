namespace CasualtiesUnknownOnline.GameState.Domains.WorldEntities;

/// <summary>
/// A one-shot trap/mechanism consumption was recorded at a world position.
/// </summary>
public sealed record TrapConsumedEvent(
	EntityPosition Position,
	int Kind,
	byte Extra,
	long TriggeredAtMs) : WorldEntityEvent;
