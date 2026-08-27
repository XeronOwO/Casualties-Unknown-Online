namespace CasualtiesUnknownOnline.GameState;

/// <summary>
/// The stable identifier of a player/game actor in the kernel. The host uses a
/// fixed value; guests use their Steam id or a simulation-local id.
/// </summary>
public readonly record struct ActorId(ulong Value);
