using System;
using System.Collections.Generic;
using CasualtiesUnknownOnline.Runtime.Protocol;

namespace CasualtiesUnknownOnline.Runtime.Session.EntitySync;

/// <summary>
/// Pure VICTIM-side enemy-attack judgment: the client the attack may have landed
/// on decides whether it did, from its own view of the enemy and its own body
/// (the 2026-09-18 ruling; the host only announces the attack). The rule mirrors
/// the game's own checks so that "what the victim's screen shows" is what the
/// victim's body takes:
/// <list type="bullet">
/// <item>a spider bite is a COLLIDER CONTACT plus the game's facing gate
/// (<c>SpiderHandler.CheckForLimbDamage</c>: the limb resolves through
/// <c>Body.LimbFromObject</c> — a Limb collider to itself, the Body collider to
/// its closest limb — and the bite needs
/// <c>Dot((body - contact).normalized, spider.up) &gt; minVectorDotToBite</c>);</item>
/// <item>a crystal lunge damages the FIRST body along
/// <c>Physics2D.RaycastAll(crystal.position, crystal.up)</c>, and the crystal
/// stops at the first ground hit (<c>CrystalEnemy.Lunge</c>).</item>
/// </list>
/// Everything here is engine-agnostic: the Game Adapter observes the contacts and
/// ray hits on the local client and this rule answers with the limb to damage, so
/// the judgment is L0-testable without Unity and holds no latency parameter.
/// </summary>
public static class EnemyAttackJudgment
{
	/// <summary>Below this the enemy-to-limb direction is degenerate (the limb sits on the enemy) and the facing gate cannot be evaluated — a touching limb then counts as faced.</summary>
	public const float DegenerateDirectionLength = 0.0001f;

	/// <summary>One of the judging client's own limbs as its own view observed it.</summary>
	public readonly struct BiteContact(int limbIndex, NetVector2 limbPosition, bool touching)
	{
		/// <summary>Index into the local body's limb array (the index the terminal-state report carries).</summary>
		public readonly int LimbIndex = limbIndex;

		/// <summary>The limb's position on this client.</summary>
		public readonly NetVector2 LimbPosition = limbPosition;

		/// <summary>True when the announced enemy's collider actually touches this limb on THIS client.</summary>
		public readonly bool Touching = touching;
	}

	/// <summary>One collider the announced lunge ray met on the judging client, in the raycast's own order.</summary>
	public readonly struct LungeHit(bool isBody, bool isLocalBody, bool isGround)
	{
		/// <summary>True when the hit resolves to a player body (<c>Utils.GetBody</c>).</summary>
		public readonly bool IsBody = isBody;

		/// <summary>True when that body is the judging client's own body.</summary>
		public readonly bool IsLocalBody = isLocalBody;

		/// <summary>True when the hit is the game's ground layer (layer 6), where the lunge stops.</summary>
		public readonly bool IsGround = isGround;
	}

	/// <summary>
	/// The limb the announced spider bite reached on this client, or -1 when it
	/// reached none. Only a TOUCHING limb is eligible (a bite the local view does
	/// not show connecting does not land — the intended semantics, not a lost
	/// command); ties keep the input order, and the nearest touching limb wins,
	/// mirroring <c>Body.LimbFromObject</c>'s closest-limb fallback. The facing
	/// gate is applied to that limb once, exactly as the game does — a limb the
	/// spider has its back to is not bitten, and the bite does not fall through to
	/// another limb.
	/// </summary>
	public static int SelectBittenLimb(NetVector2 enemyPosition, NetVector2 enemyUp, float minVectorDotToBite,
		IReadOnlyList<BiteContact> contacts)
	{
		var bestIndex = -1;
		var bestDistance = float.MaxValue;
		var bestPosition = default(NetVector2);
		for (var i = 0; i < contacts.Count; i++)
		{
			var contact = contacts[i];
			if (!contact.Touching)
			{
				continue;
			}

			var distance = Distance(enemyPosition, contact.LimbPosition);
			if (distance >= bestDistance)
			{
				continue;
			}

			bestIndex = contact.LimbIndex;
			bestDistance = distance;
			bestPosition = contact.LimbPosition;
		}

		if (bestIndex < 0)
		{
			return -1;
		}

		var toX = bestPosition.X - enemyPosition.X;
		var toY = bestPosition.Y - enemyPosition.Y;
		var length = (float)Math.Sqrt(toX * toX + toY * toY);
		if (length <= DegenerateDirectionLength)
		{
			return bestIndex;
		}

		var facing = (toX * enemyUp.X + toY * enemyUp.Y) / length;
		return facing > minVectorDotToBite ? bestIndex : -1;
	}

	/// <summary>
	/// True when the announced crystal lunge reached the judging client's own
	/// body: the game damages the FIRST body its ray meets and stops the crystal
	/// at the first ground hit, so a ground hit in front of every body (or a first
	/// body that is not this client's) means no damage here.
	/// </summary>
	public static bool LungeHitsLocalBody(IReadOnlyList<LungeHit> hits)
	{
		for (var i = 0; i < hits.Count; i++)
		{
			var hit = hits[i];
			if (hit.IsBody)
			{
				return hit.IsLocalBody;
			}

			if (hit.IsGround)
			{
				return false;
			}
		}

		return false;
	}

	private static float Distance(NetVector2 a, NetVector2 b)
	{
		var dx = a.X - b.X;
		var dy = a.Y - b.Y;
		return (float)Math.Sqrt(dx * dx + dy * dy);
	}
}
