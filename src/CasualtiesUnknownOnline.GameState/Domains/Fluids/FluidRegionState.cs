namespace CasualtiesUnknownOnline.GameState.Domains.Fluids;

/// <summary>
/// Authoritative persistent fluid-region checkpoint: a coarse region's total
/// amount and dominant type. The high-frequency simulation grid remains a
/// stream/projection; this domain stores only periodic authoritative region
/// facts.
/// </summary>
public sealed record FluidRegionState(
	int ChunkX,
	int ChunkY,
	int TotalAmount,
	byte MainType,
	long UpdatedAtMs);
