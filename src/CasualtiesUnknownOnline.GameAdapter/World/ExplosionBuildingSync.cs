using System.Collections.Generic;
using UnityEngine;

namespace CasualtiesUnknownOnline.GameAdapter.World;

/// <summary>
/// Building-entity damage diff around ANY CreateExplosion (WorldGeneration.cs:
/// 3963-4070, the building branch 3986-3995): snapshot the entities' health in
/// the prefix, report whoever lost health in the postfix — the same snapshot
/// diff as the attack patch (BodyPatches.cs:171-198). This covers EVERY
/// explosion on either side (the mine timeline, chain mines, the host's
/// applier, a player's own explosive) without intercepting the game's
/// execution: the game's explosion runs untouched, only its consequences are
/// reported. The report rides the existing BuildingEntityDamaged channel
/// (guest → host report / host → broadcast relay).
/// </summary>
internal static class ExplosionBuildingSync
{
	/// <summary>Prefix work: snapshot every entity's health (null when no session — no reporting anyway, skip the cost).</summary>
	internal static List<(BuildingEntity, float)>? Capture() => PatchBridge.Impl?.IsSessionActive == true ? CaptureActive() : null;

	private static List<(BuildingEntity, float)> CaptureActive()
	{
		var state = new List<(BuildingEntity, float)>();
		foreach (var entity in Object.FindObjectsOfType<BuildingEntity>())
		{
			state.Add((entity, entity.health));
		}

		return state;
	}

	/// <summary>Postfix work: report whoever lost health (the explosion's structural damage).</summary>
	internal static void ReportDamaged(List<(BuildingEntity, float)>? state)
	{
		if (state == null)
		{
			return;
		}

		foreach (var (entity, before) in state)
		{
			if (entity != null && entity.health < before) // Unity object — ==
			{
				PatchBridge.Impl?.OnBuildingEntityDamaged(entity, before - entity.health, playHitSound: true);
			}
		}
	}
}
