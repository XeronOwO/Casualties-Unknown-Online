using System;
using System.Reflection;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Patching;

/// <summary>
/// The enemy-stun presentation bridge contract: the GameAdapter type that
/// captures the host's native stun/stuck state and mirrors it onto the frozen
/// render copy's <see cref="RemoteEnemyDriver"/> must keep its surface stable.
/// The adapter is loaded reflectively because it references Unity/game types.
/// </summary>
public class EnemyStunPresentationTests
{
	private const string PresentationTypeName =
		"CasualtiesUnknownOnline.GameAdapter.Character.EnemyStunPresentation";
	private const string DriverTypeName =
		"CasualtiesUnknownOnline.GameAdapter.Character.RemoteEnemyDriver";

	[Fact]
	public void EnemyStunPresentation_HasCaptureAndApplySurface()
	{
		var type = GameAssemblyHost.Adapter.GetType(PresentationTypeName, throwOnError: false)
			?? throw new InvalidOperationException("EnemyStunPresentation type not found in the adapter assembly.");

		var buildingEntity = GameAssemblyHost.Game.GetType("BuildingEntity", throwOnError: false)
			?? throw new InvalidOperationException("BuildingEntity not found in the game assembly.");

		var capture = type.GetMethod("IsStunned", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);
		Assert.NotNull(capture);
		var captureParameters = capture!.GetParameters();
		Assert.Single(captureParameters);
		Assert.Equal(buildingEntity, captureParameters[0].ParameterType);
		Assert.Equal(typeof(bool), capture.ReturnType);

		var apply = type.GetMethod("Apply", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);
		Assert.NotNull(apply);
		var applyParameters = apply!.GetParameters();
		Assert.Equal(2, applyParameters.Length);
		Assert.Equal(buildingEntity, applyParameters[0].ParameterType);
		Assert.Equal(typeof(bool), applyParameters[1].ParameterType);
		Assert.Equal(typeof(bool), apply.ReturnType);
	}

	[Fact]
	public void RemoteEnemyDriver_ExposesStunnedFlag()
	{
		var driverType = GameAssemblyHost.Adapter.GetType(DriverTypeName, throwOnError: false)
			?? throw new InvalidOperationException("RemoteEnemyDriver type not found in the adapter assembly.");

		var monoBehaviour = GameAssemblyHost.ResolveType("UnityEngine.MonoBehaviour")
			?? throw new InvalidOperationException("UnityEngine.MonoBehaviour not found.");
		Assert.True(monoBehaviour.IsAssignableFrom(driverType));

		var property = driverType.GetProperty("Stunned", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
		Assert.NotNull(property);
		Assert.Equal(typeof(bool), property!.PropertyType);
		Assert.True(property.CanRead);
		Assert.True(property.CanWrite);
	}
}
