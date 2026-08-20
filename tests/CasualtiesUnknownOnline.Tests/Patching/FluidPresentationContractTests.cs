using System;
using System.Reflection;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Patching;

/// <summary>
/// The guest-side fluid-presentation replay surface contract: the adapter must
/// keep the class that turns a <see cref="FluidPresentationMsg"/> into a
/// WaterPusher / waterflow sound, and the host authority must keep the
/// push-cadence helper. The tests load the adapter reflectively (the test
/// project never compile-references GameAdapter).
/// </summary>
public class FluidPresentationContractTests
{
	[Fact]
	public void FluidPresentationApplication_Exists_AndHasApply()
	{
		var type = GameAssemblyHost.Adapter.GetType(
			"CasualtiesUnknownOnline.GameAdapter.World.FluidPresentationApplication",
			throwOnError: false)
			?? throw new InvalidOperationException("FluidPresentationApplication type not found in the adapter assembly.");

		var apply = type.GetMethod("Apply", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
		Assert.NotNull(apply);
		var parameters = apply!.GetParameters();
		Assert.Single(parameters);
		Assert.Equal(typeof(FluidPresentationMsg), parameters[0].ParameterType);
	}

	[Fact]
	public void FluidPresentationApplication_HasWaterPusherSpawner()
	{
		var type = GameAssemblyHost.Adapter.GetType(
			"CasualtiesUnknownOnline.GameAdapter.World.FluidPresentationApplication",
			throwOnError: false)
			?? throw new InvalidOperationException("FluidPresentationApplication type not found in the adapter assembly.");

		var spawn = type.GetMethod("SpawnWaterPusher", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);
		Assert.NotNull(spawn);

		var pusher = GameAssemblyHost.Game.GetType("WaterPusher", throwOnError: false)
			?? throw new InvalidOperationException("WaterPusher not found in the game assembly.");
		Assert.NotNull(pusher.GetField("direction", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic));
	}

	[Fact]
	public void FluidSimulationAuthority_KeepsPushCadenceHelper()
	{
		var type = GameAssemblyHost.Adapter.GetType(
			"CasualtiesUnknownOnline.GameAdapter.World.FluidSimulationAuthority",
			throwOnError: false)
			?? throw new InvalidOperationException("FluidSimulationAuthority type not found in the adapter assembly.");

		var helper = type.GetMethod("SendWaterPushIfDue", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
		Assert.NotNull(helper);
		var parameters = helper!.GetParameters();
		Assert.True(parameters.Length == 4,
			$"SendWaterPushIfDue must take (FluidManager, Vector2Int, Vector2, Dictionary<...>), got {parameters.Length} parameter(s)");
		Assert.Equal("FluidManager", parameters[0].ParameterType.Name);
		Assert.Equal("Vector2Int", parameters[1].ParameterType.Name);
		Assert.Equal("Vector2", parameters[2].ParameterType.Name);
		Assert.Equal("Dictionary`2", parameters[3].ParameterType.Name);
	}
}
