using System;
using System.Reflection;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Patching;

/// <summary>
/// The turret lightSprite gate contract: the replay keeps the remote turret's
/// lightSprite steady during the 0.5 s warning window and only lets the native
/// flicker start at the firing moment (TurretScript.cs:29). The gate is a
/// GameAdapter MonoBehaviour, so the test loads it reflectively and locks the
/// surface a game update could break: the type exists, derives from
/// MonoBehaviour, and exposes Begin(TurretScript) + LateUpdate.
/// </summary>
public class TurretLightSpriteGateTests
{
	[Fact]
	public void GateType_Exists_DerivesFromMonoBehaviour_AndHasBegin()
	{
		var type = GameAssemblyHost.Adapter.GetType(
			"CasualtiesUnknownOnline.GameAdapter.World.TurretLightSpriteGate",
			throwOnError: false)
			?? throw new InvalidOperationException("TurretLightSpriteGate type not found in the adapter assembly.");

		var mono = GameAssemblyHost.ResolveType("UnityEngine.MonoBehaviour")
			?? throw new InvalidOperationException("UnityEngine.MonoBehaviour not found.");
		Assert.True(mono.IsAssignableFrom(type),
			"TurretLightSpriteGate must be a MonoBehaviour so its LateUpdate runs after the game's Update.");

		var begin = type.GetMethod("Begin", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);
		Assert.NotNull(begin);
		var parameters = begin!.GetParameters();
		Assert.Single(parameters);
		var turret = GameAssemblyHost.Game.GetType("TurretScript", throwOnError: false)
			?? throw new InvalidOperationException("TurretScript not found in the game assembly.");
		Assert.Equal(turret, parameters[0].ParameterType);
	}

	[Fact]
	public void GateType_HasLateUpdate()
	{
		var type = GameAssemblyHost.Adapter.GetType(
			"CasualtiesUnknownOnline.GameAdapter.World.TurretLightSpriteGate",
			throwOnError: false)
			?? throw new InvalidOperationException("TurretLightSpriteGate type not found in the adapter assembly.");

		Assert.Contains(
			type.GetMethods(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public),
			m => m.Name == "LateUpdate");
	}

	[Fact]
	public void ApplyTurretFired_MethodStillExists()
	{
		var type = GameAssemblyHost.Adapter.GetType(
			"CasualtiesUnknownOnline.GameAdapter.World.TrapStateActions",
			throwOnError: false)
			?? throw new InvalidOperationException("TrapStateActions type not found in the adapter assembly.");

		var apply = type.GetMethod("ApplyTurretFired", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);
		Assert.NotNull(apply);
	}
}
