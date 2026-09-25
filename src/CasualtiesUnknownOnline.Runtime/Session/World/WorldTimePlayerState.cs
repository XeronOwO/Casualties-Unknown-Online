namespace CasualtiesUnknownOnline.Runtime.Session.World;

/// <summary>
/// One in-world player's inputs to the world-time SLEEP policy. StateKnown
/// false means CUO has no authoritative snapshot yet (a just-joined member) —
/// the sleep policy then refuses to accelerate over a player it cannot observe.
/// Movement is deliberately NOT an input: a movement key is an ACTION of the
/// player who pressed it, taken on that player's own client (the native rule,
/// PlayerCamera.cs:921-924), never a velocity the host polls — user ruling
/// 2026-09-18, decision 184.
/// </summary>
public readonly record struct WorldTimePlayerState(
	bool StateKnown,
	bool Alive,
	float Consciousness,
	bool BrainDying);
