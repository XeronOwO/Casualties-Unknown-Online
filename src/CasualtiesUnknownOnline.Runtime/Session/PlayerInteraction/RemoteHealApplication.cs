using System;
using System.Collections.Generic;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;

namespace CasualtiesUnknownOnline.Runtime.Session.PlayerInteraction;

/// <summary>
/// Pure application of a <see cref="RemoteHealProfile"/> to a target character
/// snapshot: pick the most injured limb and apply the dressing/medicine effect.
/// No game assembly, no state, no I/O — the same code path is used by the host
/// authority and the L0 tests.
/// </summary>
public static class RemoteHealApplication
{
	/// <summary>
	/// Pick the limb with the lowest skin+muscle health (the most injured);
	/// falls back to the first valid limb when every value is full.
	/// </summary>
	public static int PickMostInjuredLimb(IReadOnlyList<CharacterLimbMsg> limbs)
	{
		if (limbs.Count == 0)
		{
			return -1;
		}

		var best = -1;
		var bestScore = float.MaxValue;
		for (var i = 0; i < limbs.Count; i++)
		{
			var limb = limbs[i];
			if (limb.Dismembered)
			{
				continue;
			}

			var score = limb.SkinHealth + limb.MuscleHealth;
			if (best < 0 || score < bestScore)
			{
				best = i;
				bestScore = score;
			}
		}

		return best;
	}

	/// <summary>
	/// Resolve a non-negative native-UI limb selection; falls back to the
	/// most-injured automatic pick when the requested limb is absent or
	/// dismembered. -1 means auto.
	/// </summary>
	public static int ResolveLimbIndex(IReadOnlyList<CharacterLimbMsg> limbs, int requestedLimbIndex)
	{
		if (requestedLimbIndex >= 0
			&& requestedLimbIndex < limbs.Count
			&& !limbs[requestedLimbIndex].Dismembered)
		{
			return requestedLimbIndex;
		}

		return PickMostInjuredLimb(limbs);
	}

	/// <summary>
	/// Apply one medical item's full-use effect to a limb snapshot. Values are
	/// clamped to non-negative where the game's fields are non-negative counts
	/// (pain, timers, bleed); skin/muscle immediate heals stay within 0-100.
	/// </summary>
	public static void Apply(CharacterLimbMsg limb, RemoteHealProfile profile)
	{
		limb.SkinHealAmount = Math.Max(0f, limb.SkinHealAmount + profile.SkinHealAmount);
		limb.BandageSlowAmount = Math.Max(0f, limb.BandageSlowAmount + profile.BandageSlowAmount);
		limb.Pain = Math.Max(0f, limb.Pain + profile.Pain);
		limb.BoneHealTimer = Math.Max(0f, limb.BoneHealTimer + profile.BoneHealTimer);
		limb.DislocationTimer = Math.Max(0f, limb.DislocationTimer + profile.DislocationTimer);
		limb.DisinfectionTime = Math.Max(0f, limb.DisinfectionTime + profile.DisinfectionTime);
		limb.BleedAmount = Math.Max(0f, limb.BleedAmount + profile.BleedAmount);
		limb.SkinHealth = Clamp100(limb.SkinHealth + profile.SkinHealth);
		limb.MuscleHealth = Clamp100(limb.MuscleHealth + profile.MuscleHealth);
	}

	/// <summary>
	/// Apply one medical item's full-use effect to the target's health and
	/// limb snapshot. Limb effects use the existing limb-only apply; the
	/// body-level component effect (opiate amount) is clamped to non-negative
	/// because it is a count on the <c>Painkillers</c> component.
	/// </summary>
	public static void Apply(CharacterHealthMsg health, CharacterLimbMsg limb, RemoteHealProfile profile)
	{
		Apply(limb, profile);
		if (health is not null)
		{
			health.OpiateAmount = Math.Max(0f, health.OpiateAmount + profile.OpiateAmount);
		}
	}

	private static float Clamp100(float value) => Math.Max(0f, Math.Min(100f, value));
}
