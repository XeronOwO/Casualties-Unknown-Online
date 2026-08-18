using System;
using System.Reflection;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Patching;

/// <summary>
/// The remote building-destruction presentation surface (L0 reflection):
/// BuildingEntityUpdatePatch still suppresses the attacker-side drop roll on a
/// RemoteEntityDeath, and the new ReplayDestructionVisuals helper exists to
/// replay the non-drop destruction pieces (BuildingBreakParticle + DustBig +
/// the rock sound) before the entity is destroyed. This locks the patch shape
/// so a rename/removal fails in dotnet test, not in a live session.
/// </summary>
public class BuildingDestructionReplayPatchTests
{
	private static readonly Type Patch = GameAssemblyHost.Adapter.GetType(
		"CasualtiesUnknownOnline.GameAdapter.Patches.BuildingEntityUpdatePatch",
		throwOnError: true)!;

	[Fact]
	public void Prefix_RemoteDeathPath_RemainsABuildingEntityPrefix()
	{
		var prefix = Patch.GetMethod("Prefix", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public)
			?? throw new InvalidOperationException("BuildingEntityUpdatePatch.Prefix not found.");

		var parameters = prefix.GetParameters();
		Assert.True(parameters.Length == 1
			&& parameters[0].Name == "__instance"
			&& parameters[0].ParameterType.FullName == "BuildingEntity",
			$"Prefix must be (BuildingEntity __instance), got {parameters.Length} parameter(s)");
		Assert.Equal(typeof(bool), prefix.ReturnType);
	}

	[Fact]
	public void ReplayDestructionVisuals_IsTheRemotePresentationHelper()
	{
		var replay = Patch.GetMethod("ReplayDestructionVisuals", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public)
			?? throw new InvalidOperationException("BuildingEntityUpdatePatch.ReplayDestructionVisuals not found.");

		var parameters = replay.GetParameters();
		Assert.True(parameters.Length == 1
			&& parameters[0].ParameterType.FullName == "BuildingEntity",
			$"ReplayDestructionVisuals must take (BuildingEntity), got {parameters.Length} parameter(s)");
	}
}
