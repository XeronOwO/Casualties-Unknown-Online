namespace CasualtiesUnknownOnline.GameState;

/// <summary>
/// Base type for facts accepted by the kernel. Events are immutable and reduce
/// deterministically inside a committed batch.
/// </summary>
public abstract record GameEvent;
