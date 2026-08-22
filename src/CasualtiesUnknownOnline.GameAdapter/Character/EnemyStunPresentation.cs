using HarmonyLib;

namespace CasualtiesUnknownOnline.GameAdapter.Character;

/// <summary>
/// The enemy-stun presentation bridge between the native enemy state and the
/// host-authoritative <see cref="EnemyEntity.Stunned"/> flag. The host captures
/// whether an enemy is presenting its stunned/stuck state (SpiderHandler.stunTime
/// &gt; 0 — public; CrystalEnemy.stuck — private, reflected); the guest mirrors
/// the received boolean onto its <see cref="RemoteEnemyDriver"/> so the render
/// copy owns the same presentation flag without the driver mutating the enemy's
/// private AI timers (only the presentation subset travels, never the timer).
/// </summary>
internal static class EnemyStunPresentation
{
	private const string CrystalEnemyStuckFieldName = "stuck";

	/// <summary>Host capture: is this enemy currently stunned/stuck?</summary>
	internal static bool IsStunned(BuildingEntity entity)
	{
		var spider = entity.GetComponentInChildren<SpiderHandler>();
		if (spider != null) // Unity object — ==
		{
			return spider.stunTime > 0f;
		}

		var crystal = entity.GetComponentInChildren<CrystalEnemy>();
		return crystal != null && CrystalEnemyStunAccess.IsStuck(crystal); // Unity object — ==
	}

	/// <summary>
	/// Guest apply: mirror the host's stun flag onto the frozen render copy's
	/// driver. Returns true only on an actual transition so the caller can log
	/// the state change once.
	/// </summary>
	internal static bool Apply(BuildingEntity entity, bool stunned)
	{
		var driver = entity.GetComponent<RemoteEnemyDriver>();
		if (driver == null) // Unity object — ==
		{
			return false;
		}

		if (driver.Stunned == stunned)
		{
			return false;
		}

		driver.Stunned = stunned;
		return true;
	}

	private static class CrystalEnemyStunAccess
	{
		internal static bool IsStuck(CrystalEnemy enemy)
		{
			var stuck = Traverse.Create(enemy).Field(CrystalEnemyStuckFieldName).GetValue<bool>();
			return stuck;
		}
	}
}
