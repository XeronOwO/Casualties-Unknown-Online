using CasualtiesUnknownOnline.GameState.Domains.World;
using CasualtiesUnknownOnline.Runtime.Session.Items;
using CasualtiesUnknownOnline.Runtime.Session.World;
using CasualtiesUnknownOnline.Tests.Fakes;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.World;

/// <summary>
/// The host's run end is the kernel run's end too. Leaving the run state alive
/// makes the next run-start publish look like a layer advance: CommitBaseline
/// sees a current run and advances it, so a fresh world is recorded at the old
/// run's layer and the layer-end cut has no live world to read.
/// </summary>
public class RunEndKernelResetTests
{
	[Fact]
	public void HostRunEnd_DropsTheKernelRun()
	{
		using var world = ItemSimWorld.Create();
		var kernel = world.Host.Services.GetRequiredService<ItemKernelAuthority>();
		var control = world.Host.Services.GetRequiredService<IWorldControl>();
		Assert.True(kernel.TryStartRun(
			world.Host.SteamId,
			new RunState(1UL, [1, 2, 3, 4], 0, 0, 0, false, null, 0),
			out _,
			out _));

		control.PublishRunEnded(0);

		Assert.Null(kernel.QueryRun());
	}
}
