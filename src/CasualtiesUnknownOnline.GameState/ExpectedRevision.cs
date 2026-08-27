namespace CasualtiesUnknownOnline.GameState;

/// <summary>
/// A precondition that a command must observe before it may commit. The first
/// slice uses one aggregate revision per operation; cross-domain read sets are
/// added in later phases.
/// </summary>
public readonly record struct ExpectedRevision(ulong AggregateId, ulong Revision);
