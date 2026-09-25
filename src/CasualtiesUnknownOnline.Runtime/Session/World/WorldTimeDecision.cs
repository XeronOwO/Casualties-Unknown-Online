using CasualtiesUnknownOnline.Runtime.Protocol;

namespace CasualtiesUnknownOnline.Runtime.Session.World;

/// <summary>
/// The policy verdict: the speed the session must run at now, plus the request
/// value to KEEP. The sleep branch clears the request — a sleep fast-forward
/// must never re-apply itself after everyone wakes.
/// </summary>
public readonly record struct WorldTimeDecision(WorldTimeSpeed Speed, WorldTimeSpeed NextRequested);
