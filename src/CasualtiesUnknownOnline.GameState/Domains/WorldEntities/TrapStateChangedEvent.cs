namespace CasualtiesUnknownOnline.GameState.Domains.WorldEntities;

/// <summary>
/// A trap/mechanism entity changed lifecycle phase at a world position/kind.
/// </summary>
public sealed record TrapStateChangedEvent(
	EntityPosition Position,
	int Kind,
	TrapPhase Phase,
	byte Extra,
	long TransitionedAtMs) : WorldEntityEvent;
