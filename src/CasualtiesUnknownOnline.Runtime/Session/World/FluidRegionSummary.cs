namespace CasualtiesUnknownOnline.Runtime.Session.World;

/// <summary>
/// A host-derived coarse fluid-region summary ready for kernel projection. The
/// game grid is aggregated into world chunks (<c>WorldGeneration.CHUNKSIZE</c>
/// cells on each axis); only non-empty chunks are reported here. The
/// <see cref="FluidKernelProjection"/> adds the authoritative timestamp and
/// commits it as a kernel <c>FluidRegionState</c>.
/// </summary>
public sealed record FluidRegionSummary(
	int ChunkX,
	int ChunkY,
	int TotalAmount,
	byte MainType);
