using System;
using System.Reflection;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Patching;

/// <summary>
/// The CrystalEnemy wind-up telegraph bridge contract: the GameAdapter helper
/// that captures the host's pre-lunge line and mirrors it onto the frozen
/// render copy must keep its surface stable. The adapter is loaded reflectively
/// because it references Unity/game types.
/// </summary>
public class CrystalWindupPresentationTests
{
	private const string PresentationTypeName =
		"CasualtiesUnknownOnline.GameAdapter.Character.CrystalWindupPresentation";
	private const string DriverTypeName =
		"CasualtiesUnknownOnline.GameAdapter.Character.RemoteEnemyDriver";

	private static readonly Type PresentationType = GameAssemblyHost.Adapter.GetType(
		PresentationTypeName,
		throwOnError: false)
		?? throw new InvalidOperationException("CrystalWindupPresentation type not found in the adapter assembly.");

	[Fact]
	public void CrystalWindupPresentation_CaptureAmountTakesCrystalEnemy_AndReturnsFloat()
	{
		var capture = PresentationType.GetMethod("CaptureAmount", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public)
			?? throw new InvalidOperationException("CrystalWindupPresentation.CaptureAmount not found.");
		var parameters = capture.GetParameters();
		Assert.Single(parameters);
		Assert.Equal("CrystalEnemy", parameters[0].ParameterType.FullName);
		Assert.Equal(typeof(float), capture.ReturnType);
	}

	[Fact]
	public void CrystalWindupPresentation_CaptureLineEndTakesCrystalEnemy_AndReturnsNullableVector()
	{
		var capture = PresentationType.GetMethod("CaptureLineEnd", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public)
			?? throw new InvalidOperationException("CrystalWindupPresentation.CaptureLineEnd not found.");
		var parameters = capture.GetParameters();
		Assert.Single(parameters);
		Assert.Equal("CrystalEnemy", parameters[0].ParameterType.FullName);
		Assert.True(capture.ReturnType.IsGenericType);
		Assert.Equal(typeof(Nullable<>), capture.ReturnType.GetGenericTypeDefinition());
		Assert.Equal("CasualtiesUnknownOnline.Runtime.Protocol.NetVector2", capture.ReturnType.GetGenericArguments()[0].FullName);
	}

	[Fact]
	public void CrystalWindupPresentation_ApplyTakesBuildingEntityAmountAndLineEnd()
	{
		var apply = PresentationType.GetMethod("Apply", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public)
			?? throw new InvalidOperationException("CrystalWindupPresentation.Apply not found.");
		var parameters = apply.GetParameters();
		Assert.Equal(3, parameters.Length);
		Assert.Equal("BuildingEntity", parameters[0].ParameterType.FullName);
		Assert.Equal(typeof(float), parameters[1].ParameterType);
		Assert.True(parameters[2].ParameterType.IsGenericType);
		Assert.Equal(typeof(Nullable<>), parameters[2].ParameterType.GetGenericTypeDefinition());
		Assert.Equal("CasualtiesUnknownOnline.Runtime.Protocol.NetVector2", parameters[2].ParameterType.GetGenericArguments()[0].FullName);
		Assert.Equal(typeof(bool), apply.ReturnType);
	}

	[Fact]
	public void RemoteEnemyDriver_ExposesCrystalWindupAmount()
	{
		var driverType = GameAssemblyHost.Adapter.GetType(DriverTypeName, throwOnError: false)
			?? throw new InvalidOperationException("RemoteEnemyDriver type not found in the adapter assembly.");

		var monoBehaviour = GameAssemblyHost.ResolveType("UnityEngine.MonoBehaviour")
			?? throw new InvalidOperationException("UnityEngine.MonoBehaviour not found.");
		Assert.True(monoBehaviour.IsAssignableFrom(driverType));

		var property = driverType.GetProperty("CrystalWindupAmount", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
		Assert.NotNull(property);
		Assert.Equal(typeof(float), property!.PropertyType);
		Assert.True(property.CanRead);
		Assert.True(property.CanWrite);
	}
}
