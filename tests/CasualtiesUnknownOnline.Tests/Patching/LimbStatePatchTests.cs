using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Patching;

/// <summary>
/// The limb-presentation patch surface and the pure limb-visual formulas. The
/// adapter is compile-excluded from the test project (it binds game/Unity
/// assemblies), so the patch shapes and the formulas are exercised
/// reflectively — the same host as the other contract tests. The Runtime half
/// of the channel is covered by LimbStateSyncTests.
/// </summary>
public class LimbStatePatchTests
{
	private static readonly Type Patches = GameAssemblyHost.Adapter.GetType(
		"CasualtiesUnknownOnline.GameAdapter.Patches.LimbStatePatches",
		throwOnError: true)!;

	private static readonly Type Presentation = GameAssemblyHost.Adapter.GetType(
		"CasualtiesUnknownOnline.GameAdapter.Character.LimbPresentation",
		throwOnError: true)!;

	private static readonly Type Renderer = GameAssemblyHost.Adapter.GetType(
		"CasualtiesUnknownOnline.GameAdapter.Character.CloneLimbRenderer",
		throwOnError: true)!;

	private static readonly Type LimbRenderMarker = GameAssemblyHost.Adapter.GetType(
		"CasualtiesUnknownOnline.GameAdapter.Character.RemoteCloneLimbRender",
		throwOnError: true)!;

	private static IEnumerable BuildContracts()
	{
		var inventory = GameAssemblyHost.Adapter.GetType("CasualtiesUnknownOnline.GameAdapter.Patches.PatchInventory")
			?? throw new InvalidOperationException("PatchInventory type not found.");
		var build = inventory.GetMethod("BuildContracts", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public)
			?? throw new InvalidOperationException("PatchInventory.BuildContracts not found.");
		return (IEnumerable)build.Invoke(null, null)!;
	}

	private static float InvokePresentation(string method, float arg)
	{
		var info = Presentation.GetMethod(method, BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public)
			?? throw new InvalidOperationException($"LimbPresentation.{method} not found.");
		return (float)info.Invoke(null, [arg])!;
	}

	private static float InvokePresentation(string method, float arg0, float arg1)
	{
		var info = Presentation.GetMethod(method, BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public)
			?? throw new InvalidOperationException($"LimbPresentation.{method} not found.");
		return (float)info.Invoke(null, [arg0, arg1])!;
	}

	[Fact]
	public void ShaderDamageFormulas_MirrorTheGame()
	{
		Assert.True(Math.Abs(InvokePresentation("SkinDamage", 67f) - 33f) < 0.001f,
			"skin damage is 100 - skinHealth (Limb.cs:501)");
		Assert.True(Math.Abs(InvokePresentation("MuscleDamage", 20f) - 80f) < 0.001f,
			"muscle damage is 100 - muscleHealth (Limb.cs:502)");
		Assert.True(Math.Abs(InvokePresentation("InfectionPercent", 45f) - 0.45f) < 0.001f,
			"infection tint is infectionAmount * 0.01 (Limb.cs:503)");
	}

	[Fact]
	public void RemainingLimbShaderFormulas_MirrorTheGame()
	{
		Assert.True(Math.Abs(InvokePresentation("PainAmount", 80f, 10f) - 0.75f) < 0.001f,
			"pain shader = clamp01(pain*0.01 - adrenaline*0.005) (Limb.cs:506)");
		Assert.True(Math.Abs(InvokePresentation("SnowAmount", 7f, 200f) - 7f) < 0.001f,
			"snow shader = max(snowAmount, dirtyness*0.01) (Limb.cs:504)");
		Assert.True(Math.Abs(InvokePresentation("DirtynessAmount", 40f) - 0.8f) < 0.001f,
			"dirtyness shader = clamp01(dirtyness*0.02) (Limb.cs:505)");
		Assert.True(Math.Abs(InvokePresentation("WetnessAmount", 45f) - 0.45f) < 0.001f,
			"wetness shader = wetness*0.01 (Limb.cs:488)");
	}

	[Fact]
	public void BloodDripEmission_MirrorsTheGameThreshold()
	{
		var threshold = (float)Presentation.GetField("BloodDripThreshold", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public)!.GetValue(null)!;
		var rate = (float)Presentation.GetField("BloodDripRate", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public)!.GetValue(null)!;
		Assert.True(Math.Abs(threshold - 0.95f) < 0.001f, "the game's drip threshold is 0.95 (Limb.cs:463)");
		Assert.True(Math.Abs(rate - 5f) < 0.001f, "the game's drip rate is 5 (Limb.cs:465)");

		Assert.True(Math.Abs(InvokePresentation("BloodEmissionRate", 0.96f) - 5f) < 0.001f,
			"above 0.95 the drip emits at rate 5");
		Assert.True(Math.Abs(InvokePresentation("BloodEmissionRate", 0.95f)) < 0.001f,
			"at 0.95 the drip is OFF — the game's condition is strictly greater");
		Assert.True(Math.Abs(InvokePresentation("BloodEmissionRate", 0f)) < 0.001f,
			"no fur blood means no drip");
	}

	[Fact]
	public void ActiveToggle_AppliesInBothDirections()
	{
		var must = Presentation.GetMethod("MustSetActive", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public)
			?? throw new InvalidOperationException("LimbPresentation.MustSetActive not found.");
		var set = (bool)must.Invoke(null, [true, false])!;
		Assert.False(set, "already inactive for a dismembered limb — no write");
		set = (bool)must.Invoke(null, [true, true])!;
		Assert.True(set, "dismembered limb is still active — deactivate");
		set = (bool)must.Invoke(null, [false, false])!;
		Assert.True(set, "healthy limb copied from an inactive template — re-arm");
	}

	[Fact]
	public void PatchSurface_EveryLimbLatchCapturesThePreStateAndReportsOnlyTheTransition()
	{
		var expected = new[] { "BreakBonePatch", "MendBonePatch", "DislocatePatch", "UnDislocatePatch", "DismemberPatch" };
		foreach (var name in expected)
		{
			var patch = Patches.GetNestedType(name, BindingFlags.NonPublic | BindingFlags.Public)
				?? throw new InvalidOperationException($"LimbStatePatches.{name} not found.");

			var prefix = patch.GetMethod("Prefix", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public)
				?? throw new InvalidOperationException($"LimbStatePatches.{name}.Prefix not found.");
			var prefixParameters = prefix.GetParameters();
			Assert.True(prefixParameters.Length == 2
				&& prefixParameters[0].Name == "__instance"
				&& prefixParameters[0].ParameterType.FullName == "Limb"
				&& prefixParameters[1].Name == "__state"
				&& prefixParameters[1].ParameterType == typeof(bool).MakeByRefType(),
				$"{name}.Prefix must be (Limb __instance, out bool __state), got {prefixParameters.Length} parameter(s)");

			var postfix = patch.GetMethod("Postfix", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public)
				?? throw new InvalidOperationException($"LimbStatePatches.{name}.Postfix not found.");
			var postfixParameters = postfix.GetParameters();
			Assert.True(postfixParameters.Length == 2
				&& postfixParameters[0].Name == "__instance"
				&& postfixParameters[0].ParameterType.FullName == "Limb"
				&& postfixParameters[1].Name == "__state"
				&& postfixParameters[1].ParameterType == typeof(bool),
				$"{name}.Postfix must be (Limb __instance, bool __state), got {postfixParameters.Length} parameter(s)");
		}
	}

	[Fact]
	public void PatchInventory_ContainsEveryLimbLatchTarget()
	{
		var contracts = BuildContracts().Cast<object>().ToList();
		foreach (var method in new[] { "BreakBone", "MendBone", "Dislocate", "UnDislocate", "Dismember" })
		{
			var found = contracts.Any(c =>
			{
				var type = c.GetType();
				return (type.GetProperty("TargetType", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)?.GetValue(c) as string) == "Limb"
					&& (type.GetProperty("MethodName", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)?.GetValue(c) as string) == method;
			});
			Assert.True(found, $"PatchInventory must declare the Limb.{method} patch contract.");
		}
	}

	[Fact]
	public void CloneLimbRenderer_AppliesWholeSnapshotToCloneLimbs()
	{
		var apply = Renderer.GetMethod("ApplyCloneLimbs", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)
			?? throw new InvalidOperationException("CloneLimbRenderer.ApplyCloneLimbs not found.");
		var parameters = apply.GetParameters();
		Assert.True(parameters.Length == 2
			&& parameters[0].ParameterType.FullName == "Body"
			&& parameters[1].ParameterType.FullName == "CasualtiesUnknownOnline.Runtime.Protocol.Messages.CharacterDataMsg",
			$"ApplyCloneLimbs must take (Body, CharacterDataMsg), got {parameters.Length} parameter(s)");
	}

	[Fact]
	public void LimbRenderMarker_IsAStateFreeMonoBehaviour()
	{
		Assert.True(LimbRenderMarker.BaseType?.FullName == "UnityEngine.MonoBehaviour",
			$"RemoteCloneLimbRender must be a MonoBehaviour marker, got base {LimbRenderMarker.BaseType?.FullName}");
		Assert.True(LimbRenderMarker.GetFields(BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic).Length == 0,
			"RemoteCloneLimbRender must stay a pure marker — fields would make it stateful.");
	}
}
