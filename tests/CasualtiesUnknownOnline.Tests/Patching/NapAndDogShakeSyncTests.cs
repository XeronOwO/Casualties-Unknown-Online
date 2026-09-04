using System;
using System.Linq;
using System.Reflection;
using CasualtiesUnknownOnline.Protocol.Wire;
using Xunit;
using System.Collections;

namespace CasualtiesUnknownOnline.Tests.Patching;

/// <summary>
/// The nap-variant + dog-shake presentation surface. The owner's
/// <c>Body.TakeANap</c> plays either the standard or the sick/alt lay-down
/// clip pair (Body.cs:2484-2531), and <c>Body.dogShakeIntensity</c> drives the
/// water-shake offset (Body.cs:2550-2571); this test locks the pure clip
/// mapping, the local tracker/patch shape, the wire fields and the clone
/// driver state so a game update cannot silently drop the visual.
/// </summary>
public class NapAndDogShakeSyncTests
{
	private static readonly Type Presentation = GameAssemblyHost.Adapter.GetType(
		"CasualtiesUnknownOnline.GameAdapter.Character.NapPresentation",
		throwOnError: true)!;

	private static readonly Type Patch = GameAssemblyHost.Adapter.GetType(
		"CasualtiesUnknownOnline.GameAdapter.Patches.BodyNapPatch",
		throwOnError: true)!;

	private static readonly Type Tracker = GameAssemblyHost.Adapter.GetType(
		"CasualtiesUnknownOnline.GameAdapter.Character.LocalNapTracker",
		throwOnError: true)!;

	private static readonly Type Driver = GameAssemblyHost.Adapter.GetType(
		"CasualtiesUnknownOnline.GameAdapter.Character.RemoteBodyDriver",
		throwOnError: true)!;

	private static string BodyClip(byte napVariant)
	{
		var method = Presentation.GetMethod("BodyClip", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public)
			?? throw new InvalidOperationException("NapPresentation.BodyClip not found.");
		return (string)method.Invoke(null, [napVariant])!;
	}

	private static string ArmsClip(byte napVariant)
	{
		var method = Presentation.GetMethod("ArmsClip", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public)
			?? throw new InvalidOperationException("NapPresentation.ArmsClip not found.");
		return (string)method.Invoke(null, [napVariant])!;
	}

	[Fact]
	public void ClipMapping_MatchesTheGameNapCoroutines()
	{
		Assert.Equal("ExperimentLayDown", BodyClip(0));
		Assert.Equal("ArmsLayDown", ArmsClip(0));
		Assert.Equal("ExperimentLayDownAlt", BodyClip(1));
		Assert.Equal("ArmsLayDownAlt", ArmsClip(1));
	}

	[Fact]
	public void ClipMapping_UnknownVariantFallsBackToStandardLayDown()
	{
		Assert.Equal("ExperimentLayDown", BodyClip(99));
		Assert.Equal("ArmsLayDown", ArmsClip(99));
	}

	[Fact]
	public void LocalTracker_IsASmallStatefulMonoBehaviour()
	{
		Assert.Equal("UnityEngine.MonoBehaviour", Tracker.BaseType?.FullName);
		var field = Tracker.GetField("NapVariant", BindingFlags.Instance | BindingFlags.Public)
			?? throw new InvalidOperationException("LocalNapTracker.NapVariant not found.");
		Assert.Equal(typeof(byte), field.FieldType);
	}

	[Fact]
	public void RemoteDriver_HasNapVariantTransitionField()
	{
		var field = Driver.GetField("PrevNapVariant", BindingFlags.Instance | BindingFlags.Public)
			?? throw new InvalidOperationException("RemoteBodyDriver.PrevNapVariant not found.");
		Assert.Equal(typeof(byte), field.FieldType);
	}

	[Fact]
	public void EntityStateMsg_HasNapVariantAndDogShakeOnTheWire()
	{
		var nap = typeof(WirePlayerStreamState).GetProperty("NapVariant", BindingFlags.Instance | BindingFlags.Public)
			?? throw new InvalidOperationException("WirePlayerStreamState.NapVariant not found.");
		Assert.Equal(typeof(byte), nap.PropertyType);

		var shake = typeof(WirePlayerStreamState).GetProperty("DogShakeIntensity", BindingFlags.Instance | BindingFlags.Public)
			?? throw new InvalidOperationException("WirePlayerStreamState.DogShakeIntensity not found.");
		Assert.Equal(typeof(float), shake.PropertyType);
	}

	[Fact]
	public void PatchInventory_DeclaresBothNapCoroutines()
	{
		var inventory = GameAssemblyHost.Adapter.GetType("CasualtiesUnknownOnline.GameAdapter.Patches.PatchInventory")
			?? throw new InvalidOperationException("PatchInventory type not found.");
		var build = inventory.GetMethod("BuildContracts", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public)
			?? throw new InvalidOperationException("PatchInventory.BuildContracts not found.");
		var contracts = (IEnumerable)build.Invoke(null, null)!;

		var methods = contracts.Cast<object>().Select(c =>
		{
			var type = c.GetType();
			var target = type.GetProperty("TargetType", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)?.GetValue(c) as string;
			var method = type.GetProperty("MethodName", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)?.GetValue(c) as string;
			return target == "Body" ? method : null;
		}).Where(m => m != null).ToArray();

		Assert.Contains("NapCoroutine", methods);
		Assert.Contains("AltNapCoroutine", methods);
	}

	[Fact]
	public void NapPatchPrefixes_TargetBodyCoroutineStarters()
	{
		var napPatch = Patch.GetNestedType("NapCoroutinePatch", BindingFlags.NonPublic | BindingFlags.Public)
			?? throw new InvalidOperationException("BodyNapPatch.NapCoroutinePatch not found.");
		var altPatch = Patch.GetNestedType("AltNapCoroutinePatch", BindingFlags.NonPublic | BindingFlags.Public)
			?? throw new InvalidOperationException("BodyNapPatch.AltNapCoroutinePatch not found.");

		AssertPrefixTargetsBody(napPatch);
		AssertPrefixTargetsBody(altPatch);
	}

	private static void AssertPrefixTargetsBody(Type patchType)
	{
		var prefix = patchType.GetMethod("Prefix", BindingFlags.Static | BindingFlags.NonPublic)
			?? throw new InvalidOperationException($"{patchType.Name}.Prefix not found.");
		Assert.Equal(typeof(void), prefix.ReturnType);
		var parameters = prefix.GetParameters();
		Assert.True(parameters.Length == 1
			&& parameters[0].Name == "__instance"
			&& parameters[0].ParameterType.FullName == "Body",
			$"Prefix must be (Body __instance), got {parameters.Length} parameter(s)");
	}
}
