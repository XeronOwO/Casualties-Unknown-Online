using System;
using System.Linq;
using System.Reflection;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Patching;

/// <summary>
/// The gun muzzle-flash presentation surface: a remote clone never runs
/// <c>GunScript.Fire</c>, so the existing <c>CharacterSoundKind.GunFire</c>
/// event must also replay the source's <c>muzzleParticle.Play()</c> on the
/// owner's clone. These tests lock the replay helper's shape. The actual Unity
/// particle call is a runtime presentation action; the adapter-level unit face
/// is the reflected helper signature.
/// </summary>
public class MuzzleFlashReplayTests
{
	private static readonly Type Replay = GameAssemblyHost.Adapter.GetType(
		"CasualtiesUnknownOnline.GameAdapter.Character.MuzzleFlashReplay",
		throwOnError: true)!;

	[Fact]
	public void TryPlay_TakesBodyAndFirePosition_AndReturnsBool()
	{
		var method = Replay.GetMethod("TryPlay", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public)
			?? throw new InvalidOperationException("MuzzleFlashReplay.TryPlay not found.");
		var parameters = method.GetParameters();

		Assert.True(parameters.Length == 2
			&& parameters[0].ParameterType.FullName == "Body"
			&& parameters[1].ParameterType.FullName == "UnityEngine.Vector2"
			&& method.ReturnType == typeof(bool),
			$"TryPlay must be (Body body, Vector2 firePosition) -> bool, got ({string.Join(", ", parameters.Select(p => p.ParameterType.FullName))}) -> {method.ReturnType.FullName}");
	}
}
