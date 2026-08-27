namespace CasualtiesUnknownOnline.GameState;

/// <summary>
/// Explicit environment passed into <c>Execute</c>. The kernel must never read
/// ambient time, Unity, network, random, or game globals; values that a domain
/// needs are either on the command or carried here.
/// </summary>
public sealed record CommandContext(
	RunEpoch RunEpoch,
	ActorId Actor,
	long SimulationTimeMs = 0);
