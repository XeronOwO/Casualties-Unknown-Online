using System.Collections.Generic;
using CasualtiesUnknownOnline.Runtime.Protocol;
using CasualtiesUnknownOnline.Runtime.Session.EntitySync;
using UnityEngine;

namespace CasualtiesUnknownOnline.GameAdapter.Character;

/// <summary>
/// The judging client's own view of an announced enemy attack: it observes what
/// THIS screen shows — the frozen enemy copy's colliders against the local
/// body's colliders, and the crystal's own lunge ray — and hands those facts to
/// the pure victim-side rule <see cref="EnemyAttackJudgment"/>. Nothing here
/// decides whether an attack landed beyond that rule, and nothing here reads the
/// peer's latency: the judgment uses only the local picture and the local body.
/// </summary>
internal static class EnemyAttackLocalProbe
{
	/// <summary>The game's own ground layer — CrystalEnemy.Lunge stops the crystal at the first hit on it (CrystalEnemy.cs:156).</summary>
	private const int GroundLayer = 6;

	/// <summary>
	/// The local limb the announced spider bite reaches on this client, or -1.
	/// Contact is the collider overlap the game's own collision callback requires,
	/// and the limb resolution mirrors <c>Body.LimbFromObject</c> (Body.cs:3688):
	/// a Limb collider maps to itself and the Body collider to its closest limb —
	/// a limb reached through the Body collider counts as touching, because the
	/// game's own resolution treats that contact as reaching it. The facing gate is
	/// applied by the rule, exactly as the game applies it.
	/// <para>
	/// Approximation, recorded: the game resolves the limb from the ONE collision
	/// object Unity hands it, while this probe gathers every limb and lets the rule
	/// pick the nearest touching one. With several limbs in simultaneous contact the
	/// two can pick different limbs — same body, same bite, and in a 2D side view the
	/// contact patches are a limb apart at most.
	/// </para>
	/// </summary>
	internal static int ProbeBittenLimb(SpiderHandler spider, Body? body)
	{
		if (body == null) // Unity object — ==
		{
			return -1;
		}

		var spiderPosition = new Vector2(spider.transform.position.x, spider.transform.position.y);
		var spiderColliders = spider.GetComponentsInChildren<Collider2D>();
		var contacts = new List<EnemyAttackJudgment.BiteContact>();
		for (var i = 0; i < body.limbs.Length; i++)
		{
			var limb = body.limbs[i];
			if (limb == null || limb.dismembered) // Unity object — ==
			{
				continue;
			}

			contacts.Add(new EnemyAttackJudgment.BiteContact(
				i,
				ToNetVector2(limb.transform.position),
				IsTouching(limb.gameObject, spiderColliders)));
		}

		if (IsTouching(body.gameObject, spiderColliders))
		{
			var closest = body.GetClosestLimb(spiderPosition);
			var index = closest != null ? LimbIndexOf(body, closest) : -1; // Unity object — ==
			if (index >= 0)
			{
				contacts.Add(new EnemyAttackJudgment.BiteContact(index, ToNetVector2(closest!.transform.position), true));
			}
		}

		return EnemyAttackJudgment.SelectBittenLimb(
			ToNetVector2(spiderPosition), ToNetVector2(spider.transform.up), spider.minVectorDotToBite, contacts);
	}

	/// <summary>
	/// True when the announced crystal lunge reaches the local body on this
	/// client's own ray. The ray is the game's own call
	/// (<c>Physics2D.RaycastAll(crystal.position, crystal.up)</c>), so the answer
	/// is what this screen shows; the hits keep the raycast's own order because
	/// the rule mirrors the game's "first body wins, ground stops the crystal".
	/// </summary>
	internal static bool CrystalLungeHitsLocalBody(CrystalEnemy crystal, Body? body)
	{
		if (body == null) // Unity object — ==
		{
			return false;
		}

		var hits = Physics2D.RaycastAll(crystal.transform.position, crystal.transform.up);
		var facts = new List<EnemyAttackJudgment.LungeHit>(hits.Length);
		foreach (var hit in hits)
		{
			if (hit.transform == null) // Unity object — ==
			{
				continue;
			}

			var hitObject = hit.transform.gameObject;
			var isBody = Utils.GetBody(hitObject, out var hitBody);
			facts.Add(new EnemyAttackJudgment.LungeHit(
				isBody,
				isBody && hitBody == body, // Unity object — ==
				hitObject.layer == GroundLayer));
		}

		return EnemyAttackJudgment.LungeHitsLocalBody(facts);
	}

	private static bool IsTouching(GameObject candidate, Collider2D[] spiderColliders)
	{
		foreach (var own in candidate.GetComponentsInChildren<Collider2D>())
		{
			foreach (var other in spiderColliders)
			{
				if (own != null && other != null && Physics2D.Distance(own, other).isOverlapped) // Unity objects — ==
				{
					return true;
				}
			}
		}

		return false;
	}

	private static int LimbIndexOf(Body body, Limb limb)
	{
		for (var i = 0; i < body.limbs.Length; i++)
		{
			if (body.limbs[i] == limb) // Unity object — ==
			{
				return i;
			}
		}

		return -1;
	}

	private static NetVector2 ToNetVector2(Vector3 value) => new(value.x, value.y);
}
