using System.Collections.Generic;
using System.Linq;
using CasualtiesUnknownOnline.GameState.Domains.Fluids;
using CasualtiesUnknownOnline.Runtime.Session.World;
using Microsoft.Extensions.Logging;

namespace CasualtiesUnknownOnline.GameAdapter.World;

/// <summary>
/// Game Adapter-side consumer of the guest kernel fluid read projection. It
/// keeps a rebuildable coarse mirror of the authoritative fluid-region facts
/// (chunk totals/types) and logs the rebuilds, separate from the high-frequency
/// RLE grid stream the renderer uses. This gives the Game Adapter a diagnostic
/// and future local-simulation seam without changing the streamed grid path.
/// </summary>
internal sealed class FluidKernelViewSync(
	IWorldControl world,
	ILogger<FluidKernelViewSync> log)
{
	private readonly IWorldControl _world = world;
	private readonly ILogger<FluidKernelViewSync> _log = log;
	private IReadOnlyList<FluidRegionState> _kernelFacts = [];

	/// <summary>The latest rebuilt coarse fluid-region facts.</summary>
	internal IReadOnlyList<FluidRegionState> KernelFacts => _kernelFacts;

	internal void BindToSession() => _world.FluidRegionsProjected += OnProjected;

	internal void Unbind()
	{
		_world.FluidRegionsProjected -= OnProjected;
		_kernelFacts = [];
	}

	private void OnProjected(IReadOnlyList<FluidRegionState> regions)
	{
		_kernelFacts = regions;
		_log.LogInformation(
			"[FluidKernelView] guest rebuilt coarse fluid region view: {Count} region(s), total amount {Total}.",
			regions.Count,
			regions.Sum(r => r.TotalAmount));
	}
}
