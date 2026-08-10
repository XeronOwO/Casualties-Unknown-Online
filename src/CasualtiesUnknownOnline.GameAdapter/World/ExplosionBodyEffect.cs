using System.Collections.Generic;
using UnityEngine;

namespace CasualtiesUnknownOnline.GameAdapter.World;

/// <summary>
/// The explosion's REAL-BODY effect segment on the replay side (guest). The
/// host's own explosion hits the host's real body directly (the game's own
/// code); a replayed explosion (the trigger happened on another side) must hit
/// the replaying side's real body the same way — standing next to a mine
/// someone else detonated MUST hurt. Copied verbatim from the game's limb
/// segment (WorldGeneration.CreateExplosion, WorldGeneration.cs:4018-4069 —
/// the Ground linecast occlusion 4020 and every damage roll included) and the
/// local-audio segment (3971-3981). The parameters are the shared compile-time
/// constants, so both sides roll the same distribution; the rolls themselves
/// are local — the results travel as character state via the 1 Hz
/// CharacterData report (local compute, remote verify/sync).
/// Copy source: Assembly-CSharp, reverse-engineering 2026-08-10.
/// </summary>
internal static class ExplosionBodyEffect
{
	internal static void ApplyToLocalBodies(ExplosionParams param)
	{
		// The local-audio segment (WorldGeneration.cs:3971-3981): the player
		// near the blast hears it — tinnitus, eye panic, consciousness, hearing.
		if (PlayerCamera.main != null // Unity object — ==
			&& Vector2.Distance(param.position, PlayerCamera.main.body.transform.position) < param.range * 2.5f)
		{
			Sound.Play("tinnitus", Vector2.zero, true, false, null, 1f, 1f, true, true);
			var body = PlayerCamera.main.body;
			body.eyePanicTime = 1f;
			body.eyeCloseTime = 5f;
			body.eyeScareTime = 12f;
			body.consciousness = 31f;
			body.hearingLoss += Random.Range(27f, 36.6f);
			body.talker.Talk(Locale.GetCharacter("loud"), null, false, false);
			PlayerCamera.main.shaker.Shake(param.range * 20f);
		}

		var hits = Physics2D.OverlapCircleAll(param.position, param.range);
		var limbs = new List<Limb>();
		foreach (var collider in hits)
		{
			if (collider.TryGetComponent<Limb>(out var limb))
			{
				limbs.Add(limb);
			}

			if (collider.TryGetComponent<Body>(out var body))
			{
				limbs.AddRange(body.limbs);
			}
		}

		// The limb segment (WorldGeneration.cs:4018-4069), Ground-occlusion
		// included: a limb behind solid ground is safe.
		foreach (var limb in limbs)
		{
			if (Physics2D.Linecast(param.position, limb.transform.position, LayerMask.GetMask("Ground")))
			{
				continue;
			}

			var armorReduction = limb.GetArmorReduction();
			if (Random.Range(0f, 1f) < param.skinDamageChance)
			{
				limb.skinHealth -= param.skinDamage.RandomFromRange() / armorReduction;
			}

			limb.muscleHealth -= param.muscleDamage.RandomFromRange() / armorReduction;
			limb.body.shock = 100f;
			limb.body.lastTimeStepVelocity = ((Vector2)limb.body.transform.position - param.position).normalized * param.velocity;
			limb.body.Ragdoll();
			if (!limb.hasShrapnel)
			{
				limb.shrapnel = Random.value < param.shrapnelChance ? 5 : 0;
			}

			limb.DamageWearables(param.shrapnelChance);
			if (limb.isVital && Random.value < 0.5f)
			{
				limb.body.internalBleeding += param.muscleDamage.RandomFromRange() * 0.4f / armorReduction;
			}

			if (Random.Range(0f, 1f) < param.bleedChance)
			{
				limb.bleedAmount += param.bleedAmount.RandomFromRange() / armorReduction;
			}

			if (Random.Range(0f, 1f) < param.boneBreakChance / armorReduction)
			{
				limb.BreakBone();
			}

			if (Random.Range(0f, 1f) < param.dislocationChance / armorReduction)
			{
				limb.Dislocate();
			}

			if (limb.isHead)
			{
				limb.body.consciousness = 0f;
				if (Random.Range(0f, 1f) < 0.7f / armorReduction)
				{
					limb.body.brainHealth -= param.muscleDamage.RandomFromRange() / armorReduction * 0.5f;
				}

				if (Random.Range(0f, 1f) < param.disfigureChance / armorReduction)
				{
					limb.body.Disfigure();
				}

				if (Random.Range(0f, 1f) < param.disfigureChance / armorReduction)
				{
					limb.body.RemoveEye();
				}
			}
		}
	}
}
