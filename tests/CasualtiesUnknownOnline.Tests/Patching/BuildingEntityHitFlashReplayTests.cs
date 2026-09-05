using System;
using System.Reflection;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Patching;

/// <summary>
/// L0 reflection lock for the building-entity red HitFlash replay surface.
/// The native effect is Unity/renderer-side, so this locks the adapter contract
/// (the replay helper + the relay signature that carries the flag) at test time
/// instead of discovering a rename/signature drift in a live session.
/// </summary>
public class BuildingEntityHitFlashReplayTests
{
	private static readonly Type Sync = GameAssemblyHost.Adapter.GetType(
		"CasualtiesUnknownOnline.GameAdapter.World.WorldBuildingEntitySync",
		throwOnError: false)
		?? throw new InvalidOperationException("WorldBuildingEntitySync type not found in the adapter assembly.");

	private static readonly Type BuildingEntity = GameAssemblyHost.Game.GetType("BuildingEntity", throwOnError: false)
		?? throw new InvalidOperationException("BuildingEntity not found in the game assembly.");

	[Fact]
	public void ReplayHitFlash_TakesTheEntityAndReturnsVoid()
	{
		var replay = Sync.GetMethod("ReplayHitFlash", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)
			?? throw new InvalidOperationException("WorldBuildingEntitySync.ReplayHitFlash not found.");

		var parameters = replay.GetParameters();
		Assert.True(parameters.Length == 1 && parameters[0].ParameterType == BuildingEntity,
			$"ReplayHitFlash must take one BuildingEntity, got {parameters.Length} parameter(s)");
		Assert.Equal(typeof(void), replay.ReturnType);
	}

	[Fact]
	public void RemoteApplySignature_CarriesTheHitFlashFlag()
	{
		var apply = Sync.GetMethod("OnRemoteBuildingEntityDamaged", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)
			?? throw new InvalidOperationException("WorldBuildingEntitySync.OnRemoteBuildingEntityDamaged not found.");

		var parameters = apply.GetParameters();
		Assert.True(parameters.Length == 4
			&& parameters[0].ParameterType.FullName == "CasualtiesUnknownOnline.Runtime.Protocol.NetVector2"
			&& parameters[1].ParameterType == typeof(float)
			&& parameters[2].ParameterType == typeof(bool)
			&& parameters[3].ParameterType == typeof(bool),
			$"OnRemoteBuildingEntityDamaged must carry (NetVector2, float, bool playHitSound, bool playHitFlash), got {parameters.Length} parameter(s)");
	}
}
