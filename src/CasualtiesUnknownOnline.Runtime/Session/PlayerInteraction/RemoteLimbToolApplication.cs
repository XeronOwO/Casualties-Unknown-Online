using System;
using System.Collections.Generic;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;

namespace CasualtiesUnknownOnline.Runtime.Session.PlayerInteraction;

/// <summary>
/// Pure application of a <see cref="RemoteLimbToolProfile"/> to a character
/// snapshot. It applies immediate body/limb deltas and multiplicative factors.
/// A required limb (e.g. <c>chestdrain</c>) returns false when the target does
/// not have that limb, so the host can refuse before consuming the item. No
/// game assembly, no state, no I/O.
/// </summary>
public static class RemoteLimbToolApplication
{
	/// <summary>
	/// Apply one limb tool to the selected limb (or the profile's required limb)
	/// and body. Returns false when the required limb is missing.
	/// </summary>
	public static bool TryApply(
		CharacterHealthMsg health,
		IReadOnlyList<CharacterLimbMsg> limbs,
		RemoteLimbToolProfile profile,
		out int limbIndex)
	{
		limbIndex = -1;
		if (health is null || limbs.Count == 0)
		{
			return false;
		}

		if (profile.RequiredLimbIndex >= 0)
		{
			if (profile.RequiredLimbIndex >= limbs.Count)
			{
				return false;
			}

			limbIndex = profile.RequiredLimbIndex;
		}
		else
		{
			limbIndex = RemoteHealApplication.PickMostInjuredLimb(limbs);
			if (limbIndex < 0)
			{
				return false;
			}
		}

		var limb = limbs[limbIndex];
		limb.SkinHealth = Clamp100(limb.SkinHealth + profile.SkinHealth);
		limb.MuscleHealth = Clamp100(limb.MuscleHealth + profile.MuscleHealth);
		limb.Pain = Math.Max(0f, limb.Pain + profile.Pain);
		limb.BleedAmount = Math.Max(0f, limb.BleedAmount * profile.BleedAmountMultiplier + profile.BleedAmount);
		limb.BoneHealTimer = Math.Max(0f, limb.BoneHealTimer * profile.BoneHealTimerMultiplier + profile.BoneHealTimer);
		limb.DislocationTimer = Math.Max(0f, limb.DislocationTimer + profile.DislocationTimer);
		limb.SkinHealAmount = Math.Max(0f, limb.SkinHealAmount + profile.SkinHealAmount);
		limb.BandageSlowAmount = Math.Max(0f, limb.BandageSlowAmount + profile.BandageSlowAmount);
		health.BloodViscosity += profile.BloodViscosity;
		health.Hemothorax = Math.Max(0f, health.Hemothorax + profile.Hemothorax);
		health.Temperature += profile.Temperature;
		return true;
	}

	private static float Clamp100(float value) => Math.Max(0f, Math.Min(100f, value));
}
