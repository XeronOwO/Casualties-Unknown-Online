namespace CasualtiesUnknownOnline.GameState.Domains.WorldEntities;

/// <summary>
/// One authoritative trap state-machine fact. The identity is the world-cell
/// position plus the event kind, because one physical entity can expose several
/// event kinds (e.g. a turret has both <c>TurretFired</c> and
/// <c>TurretSelfDestructed</c>).
/// </summary>
public sealed record TrapStateFact(
	EntityPosition Position,
	int Kind,
	TrapPhase Phase,
	byte Extra,
	long TransitionedAtMs);
