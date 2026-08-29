namespace CasualtiesUnknownOnline.GameState.Domains.World;

/// <summary>
/// The run advanced to a new layer/world section. The event replaces the
/// authoritative run baseline with the newly captured generation state; the
/// old layer's world tables are expected to be cleared by the projection layer.
/// </summary>
public sealed record RunAdvancedEvent(RunState Run) : RunEvent;
