using HarmonyLib;
using UnityEngine;

namespace CasualtiesUnknownOnline.GameAdapter.Patches;

/// <summary>
/// Entity drop suppression on the guest. BuildingEntity's destroy-drop branch
/// (BuildingEntity.cs:56-121) rolls items from the LOCAL Random stream and
/// instantiates them — but the entity's health drops on BOTH sides (the
/// BlockDamaged stream applies the damage everywhere), so both would roll and
/// drop independently: two different drops with random states. Only the host
/// executes the branch (it rolls once, reports the items — the world-item
/// domain materializes identical ones for the guests); the guest just
/// destroys the entity. Particles/sounds/AnimalDeath (corpse spawning is an
/// entity-sync todo) are host-side effects.
/// </summary>
[HarmonyPatch(typeof(BuildingEntity), "Update")]
internal static class BuildingEntityUpdatePatch
{
	private static bool Prefix(BuildingEntity __instance)
	{
		if (PatchBridge.Impl is { IsGuestItemDropSuppressed: true } && __instance.health < 0.5f)
		{
			// The host rolls the drops and reports them; this side only removes
			// the entity (== null on Unity objects — the entity may be destroyed).
			Object.Destroy(__instance.gameObject);
			return false;
		}

		return true;
	}
}
