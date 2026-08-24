using System;
using System.Collections.Generic;
using System.Reflection;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Patching;

/// <summary>
/// The spider-enemy presentation surface. Frozen spider copies on the guest
/// never run <c>SpiderHandler.Update</c>, so the leg IK targets must be
/// captured by the host and applied on the copy; the one-shot bite claw visual
/// must be replayed both on the host (ordered remote bite) and on the victim
/// (host-ordered bite applied locally). These tests lock the helper shapes the
/// adapter exposes.
/// </summary>
public class SpiderEnemyPresentationTests
{
	private static readonly Type LegPresentation = GameAssemblyHost.Adapter.GetType(
		"CasualtiesUnknownOnline.GameAdapter.Character.SpiderLegPresentation",
		throwOnError: true)!;

	private static readonly Type ClawReplay = GameAssemblyHost.Adapter.GetType(
		"CasualtiesUnknownOnline.GameAdapter.Character.SpiderClawReplay",
		throwOnError: true)!;

	[Fact]
	public void SpiderLegPresentation_CaptureTakesSpiderHandler_AndReturnsVectorList()
	{
		var capture = LegPresentation.GetMethod("Capture", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public)
			?? throw new InvalidOperationException("SpiderLegPresentation.Capture not found.");
		var parameters = capture.GetParameters();
		Assert.Single(parameters);
		Assert.Equal("SpiderHandler", parameters[0].ParameterType.FullName);
		Assert.True(capture.ReturnType.IsGenericType);
		Assert.Equal(typeof(List<>), capture.ReturnType.GetGenericTypeDefinition());
		Assert.Equal("CasualtiesUnknownOnline.Runtime.Protocol.NetVector2", capture.ReturnType.GetGenericArguments()[0].FullName);
	}

	[Fact]
	public void SpiderLegPresentation_ApplyTakesBuildingEntityAndVectorList()
	{
		var apply = LegPresentation.GetMethod("Apply", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public)
			?? throw new InvalidOperationException("SpiderLegPresentation.Apply not found.");
		var parameters = apply.GetParameters();
		Assert.Equal(2, parameters.Length);
		Assert.Equal("BuildingEntity", parameters[0].ParameterType.FullName);
		Assert.True(parameters[1].ParameterType.IsGenericType);
		Assert.Equal(typeof(IReadOnlyList<>), parameters[1].ParameterType.GetGenericTypeDefinition());
		Assert.Equal("CasualtiesUnknownOnline.Runtime.Protocol.NetVector2", parameters[1].ParameterType.GetGenericArguments()[0].FullName);
		Assert.Equal(typeof(void), apply.ReturnType);
	}

	[Fact]
	public void SpiderClawReplay_PlayTakesSpiderHandlerAndDirection()
	{
		var play = ClawReplay.GetMethod("Play", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public)
			?? throw new InvalidOperationException("SpiderClawReplay.Play not found.");
		var parameters = play.GetParameters();
		Assert.Equal(2, parameters.Length);
		Assert.Equal("SpiderHandler", parameters[0].ParameterType.FullName);
		Assert.Equal("UnityEngine.Vector2", parameters[1].ParameterType.FullName);
		Assert.Equal(typeof(void), play.ReturnType);
	}
}
