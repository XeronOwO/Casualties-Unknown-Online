using HarmonyLib;
using UnityEngine;

namespace CasualtiesUnknownOnline.GameAdapter.Patches;

/// <summary>
/// Guest-side world items are kinematic RENDERS (ItemPositionFollow sets
/// bodyType = Kinematic — the host's physics is the only simulation). The game
/// assumes every item rigidbody stays Dynamic: LiquidAffect.Start
/// (LiquidAffect.cs:11) destroys the component when bodyType != Dynamic, and
/// Item.Update (Item.cs:151) then throws every frame on the dead reference
/// ("item throws NullReferenceException every frame after the host drops it").
/// A kinematic render keeps its affect alive but inert: Start is skipped (its
/// rb/body/limb fields stay uninitialized), FixedUpdate is skipped too (the
/// liquid logic would run against a kinematic body — its velocity writes are
/// ignored by the physics engine and would be overwritten by the position
/// stream anyway, and the liquid-6 Destroy path could diverge on the two
/// sides' fluid tables). The Kinematic check is safe: the game itself never
/// sets Kinematic on an item (every world item spawns Dynamic).
/// </summary>
internal static class LiquidAffectPatches
{
	[HarmonyPatch(typeof(LiquidAffect), "Start")]
	internal static class LiquidAffectStartPatch
	{
		private static bool Prefix(LiquidAffect __instance)
		{
			var rb = __instance.GetComponent<Rigidbody2D>();
			if (rb != null && rb.bodyType == RigidbodyType2D.Kinematic) // Unity object — ==
			{
				return false; // a kinematic render — no liquid behaviour to initialize
			}

			return true;
		}
	}

	[HarmonyPatch(typeof(LiquidAffect), "FixedUpdate")]
	internal static class LiquidAffectFixedUpdatePatch
	{
		// The field Start fills with the component it dereferences. When Start is skipped for a kinematic render,
		// this stays null; a later Rigidbody2D (or a Dynamic bodyType) must not make the vanilla body run against
		// the rest of the also-null fields.
		private static readonly AccessTools.FieldRef<LiquidAffect, Rigidbody2D> RbField =
			AccessTools.FieldRefAccess<LiquidAffect, Rigidbody2D>("rb");

		private static bool Prefix(LiquidAffect __instance)
		{
			// A destroyed item can still receive one last FixedUpdate, and the world/fluid manager can already be
			// gone while that callback runs. The vanilla body dereferences all three; there is nothing to simulate.
			if (__instance == null || WorldGeneration.world == null || FluidManager.main == null) // Unity objects — ==
			{
				return false;
			}

			var rb = RbField(__instance);
			return rb != null && rb.bodyType == RigidbodyType2D.Dynamic;
		}
	}
}
