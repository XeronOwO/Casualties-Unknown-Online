using System;
using System.Reflection;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Patching;

/// <summary>
/// L0 reflection surface for the remote animal-death presentation replay: the
/// replay helper exists and takes a BuildingEntity, the live-only marker flag
/// exists on RemoteEntityDeath, and the remote destruction replay continues to
/// run through the same patch helper. This locks the shape so a rename/removal
/// fails in dotnet test rather than in a live session.
/// </summary>
public class AnimalDeathReplayPatchTests
{
	private static readonly Type ReplayType = GameAssemblyHost.Adapter.GetType(
		"CasualtiesUnknownOnline.GameAdapter.World.AnimalDeathReplay",
		throwOnError: true)!;

	private static readonly Type PatchType = GameAssemblyHost.Adapter.GetType(
		"CasualtiesUnknownOnline.GameAdapter.Patches.BuildingEntityUpdatePatch",
		throwOnError: true)!;

	private static readonly Type DeathMarkerType = GameAssemblyHost.Adapter.GetType(
		"CasualtiesUnknownOnline.GameAdapter.World.RemoteEntityDeath",
		throwOnError: true)!;

	[Fact]
	public void AnimalDeathReplay_ReplayTakesBuildingEntity()
	{
		var replay = ReplayType.GetMethod("Replay",
			BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public)
			?? throw new InvalidOperationException("AnimalDeathReplay.Replay not found.");

		var parameters = replay.GetParameters();
		Assert.True(parameters.Length == 1
			&& parameters[0].ParameterType.FullName == "BuildingEntity",
			$"Replay must take (BuildingEntity), got {parameters.Length} parameter(s)");
	}

	[Fact]
	public void RemoteEntityDeath_HasLiveReplayFlag()
	{
		var property = DeathMarkerType.GetProperty("ReplayAnimalDeath",
			BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
			?? throw new InvalidOperationException("RemoteEntityDeath.ReplayAnimalDeath not found.");

		Assert.True(property.PropertyType == typeof(bool), "ReplayAnimalDeath must be a bool.");
		Assert.True(property.CanRead && property.CanWrite, "ReplayAnimalDeath must be readable and writable.");
	}

	[Fact]
	public void BuildingEntityUpdatePatch_StillReplaysDestructionVisuals()
	{
		var replay = PatchType.GetMethod("ReplayDestructionVisuals",
			BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public)
			?? throw new InvalidOperationException("BuildingEntityUpdatePatch.ReplayDestructionVisuals not found.");

		var parameters = replay.GetParameters();
		Assert.True(parameters.Length == 1
			&& parameters[0].ParameterType.FullName == "BuildingEntity",
			$"ReplayDestructionVisuals must take (BuildingEntity), got {parameters.Length} parameter(s)");
	}
}
