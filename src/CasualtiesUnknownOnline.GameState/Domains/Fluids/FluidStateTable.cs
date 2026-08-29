using System.Collections.Generic;
using System.Linq;

namespace CasualtiesUnknownOnline.GameState.Domains.Fluids;

/// <summary>
/// Immutable persistent fluid-region table.
/// </summary>
public sealed record FluidStateTable(IReadOnlyList<FluidRegionState> Regions)
{
	public static readonly FluidStateTable Empty = new([]);

	public FluidStateTable Upsert(FluidRegionState state) =>
		this with
		{
			Regions = [.. Regions.Where(r => r.ChunkX != state.ChunkX || r.ChunkY != state.ChunkY), state],
		};
}
