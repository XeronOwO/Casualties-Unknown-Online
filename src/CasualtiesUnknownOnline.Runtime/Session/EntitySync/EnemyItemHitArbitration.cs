using System;
using System.Collections.Generic;
using CasualtiesUnknownOnline.Runtime.Protocol;

namespace CasualtiesUnknownOnline.Runtime.Session.EntitySync;

/// <summary>
/// Pure host-side item-vs-enemy hit arbitration. The game's native
/// SpiderHandler.OnCollisionEnter2D item branch (SpiderHandler.cs:246-258)
/// only runs when the enemy is within 50 units of the LOCAL body — a
/// single-player scoping that breaks when a guest throws an item far from the
/// host's own body. This machine generalizes that proximity rule to every
/// in-world player (host body + remote entity-stream positions) and keeps the
/// damage/stun formulas identical to the native branch, L0-testable without
/// Unity.
/// </summary>
public static class EnemyItemHitArbitration
{
	/// <summary>The native item-hit proximity radius (SpiderHandler.cs:247).</summary>
	public const float PlayerRadius = 50f;

	/// <summary>The native minimum relative collision speed (SpiderHandler.cs:247).</summary>
	public const float MinImpactSpeed = 2f;

	/// <summary>The native item-mass clamp (SpiderHandler.cs:249).</summary>
	public const float MaxItemMass = 4f;

	/// <summary>The native health-damage factor (SpiderHandler.cs:254).</summary>
	public const float HealthFactor = 0.66f;

	/// <summary>The native stun-damage factor (SpiderHandler.cs:256).</summary>
	public const float StunFactor = 1.5f;

	/// <summary>
	/// The raw impact weight used by both damage formulas:
	/// <c>impactSpeed * Clamp(itemMass, 0, 4)</c> (SpiderHandler.cs:249).
	/// </summary>
	public static float ComputeImpactWeight(float impactSpeed, float itemMass) =>
		impactSpeed * Math.Max(0f, Math.Min(MaxItemMass, itemMass));

	/// <summary>The enemy health damage of one item hit (native: <c>num * 0.66f</c>).</summary>
	public static float ComputeHealthDamage(float impactSpeed, float itemMass) =>
		ComputeImpactWeight(impactSpeed, itemMass) * HealthFactor;

	/// <summary>The stun damage fed to <c>AnimalHit</c> (native: <c>num * 1.5f</c>).</summary>
	public static float ComputeStunDamage(float impactSpeed, float itemMass) =>
		ComputeImpactWeight(impactSpeed, itemMass) * StunFactor;

	/// <summary>Whether the collision is a reportable item impact at all: the native speed gate (<c>&gt; 2</c>).</summary>
	public static bool IsImpactEligible(float impactSpeed) => impactSpeed > MinImpactSpeed;

	/// <summary>
	/// The multiplayer generalization of the native proximity guard: is any
	/// in-world player (host or remote) within <paramref name="radius"/> of the
	/// enemy? Ties/order irrelevant because this is a boolean.
	/// </summary>
	public static bool AnyPlayerWithin(IEnumerable<NetVector2> playerPositions, NetVector2 enemyPosition, float radius)
	{
		foreach (var player in playerPositions)
		{
			var dx = player.X - enemyPosition.X;
			var dy = player.Y - enemyPosition.Y;
			if ((float)Math.Sqrt(dx * dx + dy * dy) < radius)
			{
				return true;
			}
		}

		return false;
	}
}
