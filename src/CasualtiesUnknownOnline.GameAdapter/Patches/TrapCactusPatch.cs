using CasualtiesUnknownOnline.GameAdapter.Character;
using CasualtiesUnknownOnline.Runtime.Protocol;
using UnityEngine;
using HarmonyLib;

namespace CasualtiesUnknownOnline.GameAdapter.Patches;

/// <summary>
/// Cactus → CactusHit (repeatable): a body bumped it (CactusScript.cs — gore
/// sound + the local body's knock/damage; the cactus takes 30 self-damage).
/// The event replays the gore sound; the self-damage now rides the existing
/// BuildingEntityDamaged channel as a SILENT damage report (playHitSound =
/// false — the trigger side never plays the entity's hitSound, only the
/// player-local gore sound), so the peers' cactus health and death stay in
/// sync. Remote clone bodies are excluded: only a real local body can be the
/// trigger.
/// </summary>
[HarmonyPatch(typeof(CactusScript), "OnCollisionEnter2D")]
internal static class TrapCactusPatch
{
	/// <summary>The cactus self-damage per body bump — CactusScript.cs:15 (<c>base.GetComponent&lt;BuildingEntity&gt;().health -= 30f</c>).</summary>
	private const float SelfDamagePerHit = 30f;

	private static void Postfix(CactusScript __instance, Collision2D collision)
	{
		if (collision.gameObject.GetComponentInParent<RemoteBodyDriver>() != null)
		{
			return; // a render clone's collision is not a real trigger
		}

		if (!collision.gameObject.TryGetComponent<Body>(out _))
		{
			return; // only the body branch has a visible effect
		}

		var entity = __instance.GetComponent<BuildingEntity>();
		if (entity != null) // Unity object — ==
		{
			PatchBridge.Impl?.OnBuildingEntityDamaged(entity, SelfDamagePerHit, playHitSound: false);
		}

		PatchBridge.Impl?.OnTrapTriggered(EntityEventKind.CactusHit, __instance.transform.position, 0);
	}
}
