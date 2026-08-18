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
/// Opened) and just destroys the entity. The remote side still replays the
/// pure-visual destruction pieces (BuildingBreakParticle + DustBig + the rock
/// sound — BuildingEntity.cs:58-73) so the death reads identically to the
/// attacker; drops and AnimalDeath/corpse spawning stay attacker-side.
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
			// destroyed), after replaying the same destruction visuals/sound.
			ReplayDestructionVisuals(__instance);
			Object.Destroy(__instance.gameObject);
			return false;
		}

		return true;
	}

	/// <summary>
	/// The remote side's destruction replay — the non-drop part of
	/// BuildingEntity.Update's death branch (BuildingEntity.cs:58-73), kept
	/// identical so the peer sees the break particles, dust and rock sound that
	/// the attacker's side saw. Pure presentation: none of these objects is an
	/// item or a building entity, so the shared domains never see them.
	/// </summary>
	private static void ReplayDestructionVisuals(BuildingEntity entity)
	{
		if (entity.TryGetComponent(out SpriteRenderer spriteRenderer))
		{
			var particle = Object.Instantiate(Resources.Load("BuildingBreakParticle"), entity.transform.position, entity.transform.rotation) as GameObject;
			if (particle != null) // Unity object — ==
			{
				var particleSystem = particle.GetComponent<ParticleSystem>();
				if (particleSystem != null) // Unity object — ==
				{
					var shape = particleSystem.shape;
					shape.texture = spriteRenderer.sprite != null ? spriteRenderer.sprite.texture : null; // Unity object — !=
					shape.sprite = spriteRenderer.sprite;
					particleSystem.Play();
				}
			}
		}

		Object.Instantiate(Resources.Load<GameObject>("DustBig"), entity.transform.position, Quaternion.identity);
		Sound.Play("footstep/Rock/11", entity.transform.position, false, true, null, 1f, 1f, false, false);
	}
}
