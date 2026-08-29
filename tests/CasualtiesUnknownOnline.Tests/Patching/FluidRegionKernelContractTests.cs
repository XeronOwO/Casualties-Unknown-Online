using System;
using System.Reflection;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Patching;

/// <summary>
/// The host fluid-region kernel checkpoint surface contract: the adapter must
/// keep the low-cadence aggregator that feeds the kernel fluid table from the
/// Unity grid. Loaded reflectively because the test project never
/// compile-references GameAdapter.
/// </summary>
public class FluidRegionKernelContractTests
{
	[Fact]
	public void FluidRegionKernelSync_Exists_AndHasUpdate()
	{
		var type = GameAssemblyHost.Adapter.GetType(
			"CasualtiesUnknownOnline.GameAdapter.World.FluidRegionKernelSync",
			throwOnError: false)
			?? throw new InvalidOperationException("FluidRegionKernelSync type not found in the adapter assembly.");

		var update = type.GetMethod("Update", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
		Assert.NotNull(update);
	}
}
