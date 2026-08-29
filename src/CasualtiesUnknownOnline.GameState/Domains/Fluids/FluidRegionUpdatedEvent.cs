namespace CasualtiesUnknownOnline.GameState.Domains.Fluids;

/// <summary>
/// A persistent fluid-region checkpoint was updated.
/// </summary>
public sealed record FluidRegionUpdatedEvent(FluidRegionState State) : FluidEvent;
