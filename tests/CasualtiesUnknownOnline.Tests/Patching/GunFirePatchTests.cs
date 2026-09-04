using System;
using System.Linq;
using System.Reflection;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using Xunit;
using System.Collections;

namespace CasualtiesUnknownOnline.Tests.Patching;

/// <summary>
/// The #193 weapon-fire surface: a GunScript.Fire postfix reports the owner's
/// gun shot as a CharacterSoundKind.GunFire event carrying the fire-sound clip
/// name and the recoil kick. The recoil replay itself lives in the adapter's
/// CharacterSoundSync; these tests lock the patch shape, the contract, and the
/// protocol field the wire carries.
/// </summary>
public class GunFirePatchTests
{
	private static readonly Type Patch = GameAssemblyHost.Adapter.GetType(
		"CasualtiesUnknownOnline.GameAdapter.Patches.GunFirePatch",
		throwOnError: true)!;

	[Fact]
	public void PatchSurface_TargetsGunScriptFireWithPostfix()
	{
		var postfix = Patch.GetMethod("Postfix", BindingFlags.Static | BindingFlags.NonPublic)
			?? throw new InvalidOperationException("GunFirePatch.Postfix not found.");
		var parameters = postfix.GetParameters();
		Assert.True(parameters.Length == 1 && parameters[0].Name == "__instance"
			&& parameters[0].ParameterType.FullName == "GunScript",
			$"Postfix must have exactly one __instance of GunScript, got {parameters.Length} parameter(s)");
	}

	[Fact]
	public void PatchInventory_ContainsTheGunFireContract()
	{
		var inventory = GameAssemblyHost.Adapter.GetType("CasualtiesUnknownOnline.GameAdapter.Patches.PatchInventory")
			?? throw new InvalidOperationException("PatchInventory type not found.");
		var build = inventory.GetMethod("BuildContracts", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public)
			?? throw new InvalidOperationException("PatchInventory.BuildContracts not found.");
		var contracts = (IEnumerable)build.Invoke(null, null)!;
		var found = contracts.Cast<object>().Any(c =>
		{
			var type = c.GetType();
			var target = type.GetProperty("TargetType", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)?.GetValue(c) as string;
			var method = type.GetProperty("MethodName", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)?.GetValue(c) as string;
			return target == "GunScript" && method == "Fire";
		});

		Assert.True(found, "PatchInventory must declare the GunScript.Fire patch contract (#193).");
	}

	[Fact]
	public void GunFireKind_AndRecoilField_AreOnTheWireContract()
	{
		Assert.True(Enum.IsDefined(typeof(CharacterSoundKind), CharacterSoundKind.GunFire), "CharacterSoundKind.GunFire must be defined.");
		var msg = typeof(CharacterSoundMsg);
		var recoil = msg.GetProperty("RecoilDegrees", BindingFlags.Instance | BindingFlags.Public)
			?? throw new InvalidOperationException("CharacterSoundMsg.RecoilDegrees must exist for the #193 recoil field.");
		Assert.Equal(typeof(float), recoil.PropertyType);
	}
}
