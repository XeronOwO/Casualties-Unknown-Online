using HarmonyLib;
using UnityEngine;

using CasualtiesUnknownOnline.GameAdapter.World;

namespace CasualtiesUnknownOnline.GameAdapter.Patches;

/// <summary>
/// Entity drop suppression on the non-attacker side. BuildingEntity's
/// destroy-drop branch (BuildingEntity.cs:56-121) rolls items from the LOCAL
/// Random stream and instantiates them — and the entity's health drops on
/// EVERY side (each side's attack writes locally, the damage stream applies
/// everywhere), so several sides could roll independent drops with different
/// random states. Only the attacker's side executes the branch (it rolls
/// once, locally, and reports the items — the world-item domain materializes
/// identical ones for the peers); every side whose death was applied remotely
/// is marked with RemoteEntityDeath (GameAdapter.OnRemoteBuildingEntityDamaged/
/// Opened) and just destroys the entity. Particles/sounds/AnimalDeath (corpse
/// spawning is an entity-sync todo) are attacker-side effects.
/// </summary>
[HarmonyPatch(typeof(BuildingEntity), "Update")]
internal static class BuildingEntityUpdatePatch
{
	private static bool Prefix(BuildingEntity __instance)
	{
		if (__instance.health < 0.5f && __instance.GetComponent<RemoteEntityDeath>() != null) // Unity object — ==
		{
			// The attacker rolls the drops and reports them; this side only
			// removes the entity (== null on Unity objects — the entity may be
			// destroyed).
			Object.Destroy(__instance.gameObject);
			return false;
		}

		return true;
	}
}
