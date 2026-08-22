using UnityEngine;

namespace CasualtiesUnknownOnline.GameAdapter;

/// <summary>
/// The enemy-domain half of <see cref="GameAdapter"/>'s IPatchBridge surface
/// (the partial split at the 600-line gate): one-line forwards for the enemy
/// sync / combat / proximity domains. All state stays in the main partial
/// declaration.
/// </summary>
public sealed partial class GameAdapter
{
	void IPatchBridge.OnEnemyBite(Limb limb) => _enemySync.ReportEnemyBite(limb);

	void IPatchBridge.OnSpiderTargetDecided(SpiderHandler spider) => _enemyCombat.OnSpiderTargetDecided(spider);

	void IPatchBridge.OnCrystalEnemyBodyResolved(CrystalEnemy enemy, ref Body body) => _enemyCombat.ResolveCrystalTargetBody(enemy, ref body);

	object? IPatchBridge.OnCrystalLungeBegin(CrystalEnemy enemy) => _enemyCombat.OnCrystalLungeBegin(enemy);

	void IPatchBridge.OnCrystalLungeEnd(object? state) => _enemyCombat.OnCrystalLungeEnd(state);

	float? IPatchBridge.OnEnemyItemCollision(SpiderHandler spider, Collision2D collision)
	{
		var damage = _enemyCombat.OnEnemyItemCollision(spider, collision);
		if (damage is { } healthDamage)
		{
			var entity = spider.GetComponentInParent<BuildingEntity>();
			if (entity != null) // Unity object — ==
			{
				_worldEventSync.OnBuildingEntityDamaged(entity, healthDamage, playHitSound: false);
			}
		}

		return damage;
	}

	void IPatchBridge.OnElderHorrorTick(Body body) => _enemyProximity.ReportElderHorrorTick(body);

	void IPatchBridge.OnElderHorrorDefeat(Body body) => _enemyProximity.ReportElderHorrorDefeat(body);

	void IPatchBridge.OnXalorisSepticTick(Body body) => _enemyProximity.ReportXalorisSepticTick(body);

	void IPatchBridge.OnGrabberGrabbed(Body body) => _enemyProximity.ReportGrabberGrabbed(body);
}
