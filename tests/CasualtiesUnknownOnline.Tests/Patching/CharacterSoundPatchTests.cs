using System;
using System.Collections;
using System.Reflection;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Patching;

/// <summary>
/// The character-sound chain's adapter surface, exercised reflectively (the
/// adapter is compile-excluded from the test project): the capture scopes,
/// the report surface and the patch-contract declarations. The Runtime half
/// is covered by CharacterSoundPolicyTests / CharacterSoundSyncTests.
/// </summary>
public class CharacterSoundPatchTests
{
	private static readonly Type BodyPatches = GameAssemblyHost.Adapter.GetType(
		"CasualtiesUnknownOnline.GameAdapter.Patches.BodyPatches",
		throwOnError: true)!;

	private static readonly Type BodyItemPatches = GameAssemblyHost.Adapter.GetType(
		"CasualtiesUnknownOnline.GameAdapter.Patches.BodyItemPatches",
		throwOnError: true)!;

	private static readonly Type SoundSync = GameAssemblyHost.Adapter.GetType(
		"CasualtiesUnknownOnline.GameAdapter.Character.CharacterSoundSync",
		throwOnError: true)!;

	private static IEnumerable BuildContracts()
	{
		var inventory = GameAssemblyHost.Adapter.GetType("CasualtiesUnknownOnline.GameAdapter.Patches.PatchInventory")
			?? throw new InvalidOperationException("PatchInventory type not found.");
		var build = inventory.GetMethod("BuildContracts", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public)
			?? throw new InvalidOperationException("PatchInventory.BuildContracts not found.");
		return (IEnumerable)build.Invoke(null, null)!;
	}

	private static bool HasContract(string targetType, string methodName)
	{
		foreach (var contract in BuildContracts())
		{
			var type = contract.GetType();
			if ((type.GetProperty("TargetType", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)?.GetValue(contract) as string) == targetType
				&& (type.GetProperty("MethodName", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)?.GetValue(contract) as string) == methodName)
			{
				return true;
			}
		}

		return false;
	}

	[Fact]
	public void PatchInventory_DeclaresEveryCharacterSoundTarget()
	{
		Assert.True(HasContract("Sound", "Play"), "the string Sound.Play capture contract must be declared");
		Assert.True(HasContract("Body", "Attack"), "the Body.Attack capture-scope contract must be declared");
		Assert.True(HasContract("Body", "ThrowItem"), "the Body.ThrowItem capture-scope contract must be declared");
		Assert.True(HasContract("Body", "TryExertSound"), "the Body.TryExertSound capture-scope contract must be declared");
	}

	[Fact]
	public void TryExertSoundPatch_OpensAndClosesTheExertScope()
	{
		var patch = BodyPatches.GetNestedType("BodyTryExertSoundPatch", BindingFlags.NonPublic | BindingFlags.Public)
			?? throw new InvalidOperationException("BodyPatches.BodyTryExertSoundPatch not found.");

		var prefix = patch.GetMethod("Prefix", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public)
			?? throw new InvalidOperationException("Prefix not found.");
		var prefixParameters = prefix.GetParameters();
		Assert.True(prefixParameters.Length == 2
			&& prefixParameters[0].Name == "__instance"
			&& prefixParameters[0].ParameterType.FullName == "Body"
			&& prefixParameters[1].Name == "__state"
			&& prefixParameters[1].ParameterType == typeof(IDisposable).MakeByRefType(),
			$"TryExertSound.Prefix must be (Body __instance, out IDisposable? __state), got {prefixParameters.Length} parameter(s)");

		var postfix = patch.GetMethod("Postfix", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public)
			?? throw new InvalidOperationException("Postfix not found.");
		var postfixParameters = postfix.GetParameters();
		Assert.True(postfixParameters.Length == 1
			&& postfixParameters[0].Name == "__state"
			&& postfixParameters[0].ParameterType == typeof(IDisposable),
			$"TryExertSound.Postfix must be (IDisposable? __state), got {postfixParameters.Length} parameter(s)");
	}

	[Fact]
	public void ThrowItemPatch_OpensTheThrowScopeAroundTheNativeSound()
	{
		var patch = BodyItemPatches.GetNestedType("ThrowItemPatch", BindingFlags.NonPublic | BindingFlags.Public)
			?? throw new InvalidOperationException("BodyItemPatches.ThrowItemPatch not found.");
		var state = patch.GetNestedType("ThrowState", BindingFlags.NonPublic | BindingFlags.Public)
			?? throw new InvalidOperationException("ThrowItemPatch.ThrowState not found.");

		var scopeField = state.GetField("SoundScope", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
		Assert.True(scopeField != null && scopeField.FieldType == typeof(IDisposable),
			"ThrowState must carry the IDisposable sound scope");

		var prefix = patch.GetMethod("Prefix", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public)
			?? throw new InvalidOperationException("Prefix not found.");
		var prefixParameters = prefix.GetParameters();
		Assert.True(prefixParameters.Length == 2
			&& prefixParameters[0].Name == "__instance"
			&& prefixParameters[0].ParameterType.FullName == "Body"
			&& prefixParameters[1].Name == "__state",
			$"ThrowItem.Prefix must be (Body __instance, out ThrowState __state), got {prefixParameters.Length} parameter(s)");
	}

	[Fact]
	public void SoundSyncReport_TakesTheFullCaptureFact()
	{
		var report = SoundSync.GetMethod("Report", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)
			?? throw new InvalidOperationException("CharacterSoundSync.Report not found.");
		var parameters = report.GetParameters();
		Assert.True(parameters.Length == 7
			&& parameters[0].ParameterType.FullName == "CasualtiesUnknownOnline.Runtime.Protocol.Messages.CharacterSoundKind"
			&& parameters[1].ParameterType == typeof(string)
			&& parameters[2].ParameterType.FullName == "UnityEngine.Vector2"
			&& parameters[3].ParameterType == typeof(float)
			&& parameters[4].ParameterType == typeof(bool)
			&& parameters[5].ParameterType == typeof(bool)
			&& parameters[6].ParameterType == typeof(float),
			$"CharacterSoundSync.Report must take (kind, clip, pos, volume, followOwner, twoDimensional, recoilDegrees), got {parameters.Length} parameter(s)");
	}

	[Fact]
	public void SoundSync_SubscribesToTheCharacterDataEvent()
	{
		var bind = SoundSync.GetMethod("BindToSession", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)
			?? throw new InvalidOperationException("CharacterSoundSync.BindToSession not found.");
		Assert.Empty(bind.GetParameters());
	}
}
