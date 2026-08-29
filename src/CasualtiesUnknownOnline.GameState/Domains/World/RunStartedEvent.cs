namespace CasualtiesUnknownOnline.GameState.Domains.World;

/// <summary>
/// A new run started in the current kernel epoch. The event carries the full
/// run baseline: guests replay this batch before world generation to reproduce
/// the host's Random.state and run settings.
/// </summary>
public sealed record RunStartedEvent(RunState Run) : RunEvent;
