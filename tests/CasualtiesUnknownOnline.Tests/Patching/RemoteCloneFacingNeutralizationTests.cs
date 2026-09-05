using System;
using System.Reflection;
using System.Runtime.Serialization;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Patching;

/// <summary>
/// Regression test for the remote-clone facing/head-orientation desync.
/// <c>Body.HandleVisuals</c> flips a character's facing when the synced look
/// target crosses to the opposite side AND either <c>moveDir</c> is nonzero or
/// <c>attackCooldown</c> is positive (Body.cs:3131-3135). Render clones do not
/// run the original <c>Body.Update</c>, so a stale template/inherited
/// <c>attackCooldown</c> never decays and can auto-flip a remote clone away
/// from the owner's actual facing. The proxy visual path must neutralize that
/// input before <c>HandleVisuals</c> runs.
/// </summary>
public class RemoteCloneFacingNeutralizationTests
{
	[Fact]
	public void NeutralizePoseInputs_ClearsStaleAttackCooldown()
	{
		var bodyType = GameAssemblyHost.Game.GetType("Body", throwOnError: true)!;
		var limbType = GameAssemblyHost.Game.GetType("Limb", throwOnError: true)!;
		var patchType = GameAssemblyHost.Adapter.GetType(
			"CasualtiesUnknownOnline.GameAdapter.Patches.BodyUpdatePatch",
			throwOnError: true)!;

		var body = FormatterServices.GetUninitializedObject(bodyType);

		var limbs = bodyType.GetField("limbs", BindingFlags.Public | BindingFlags.Instance)
			?? throw new InvalidOperationException("Body.limbs not found.");
		limbs.SetValue(body, Array.CreateInstance(limbType, 0));

		var attackCooldown = bodyType.GetField("attackCooldown", BindingFlags.Public | BindingFlags.Instance)
			?? throw new InvalidOperationException("Body.attackCooldown not found.");
		attackCooldown.SetValue(body, 5f);

		var eatTime = bodyType.GetField("eatTime", BindingFlags.Public | BindingFlags.Instance)
			?? throw new InvalidOperationException("Body.eatTime not found.");
		eatTime.SetValue(body, 1f);

		var neutralizer = patchType.GetMethod(
			"NeutralizePoseInputs",
			BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public)
			?? throw new InvalidOperationException("BodyUpdatePatch.NeutralizePoseInputs not found.");

		neutralizer.Invoke(null, [body]);

		Assert.Equal(0f, (float)attackCooldown.GetValue(body)!);
		Assert.Equal(0f, (float)eatTime.GetValue(body)!);
	}
}
