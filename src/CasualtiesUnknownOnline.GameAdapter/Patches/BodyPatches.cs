using System;
using System.Collections.Generic;
using System.Reflection;
using CasualtiesUnknownOnline.GameAdapter.Character;
using CasualtiesUnknownOnline.GameAdapter.Items;
using HarmonyLib;
using UnityEngine;

namespace CasualtiesUnknownOnline.GameAdapter.Patches;

/// <summary>
/// Body patches: render proxies are visual-only. Physics (FixedUpdate) and the
/// whole per-frame simulation (Body.Update — health/temperature/radiation,
/// ground queries that can damage blocks under the clone (Body.cs:2702-2711),
/// sounds (clones were audible to the local player), RNG consumption) are
/// skipped. Only the animator-driven visuals (HandleVisuals, Body.cs:3123+)
/// plus the few visual-input fields it reads (grounded, crouchAmount) are
/// maintained. See docs/game-internals.md §Clone &amp; Render Chain.
/// </summary>
internal static class BodyPatches
{
	[HarmonyPatch(typeof(Body), "FixedUpdate")]
	internal static class BodyFixedUpdatePatch
	{
		// GetComponentInParent: the driver lives on the Body GameObject.
		// == null: Unity object (a missing component is managed-null, same check).
		private static bool Prefix(Body __instance) => __instance.GetComponentInParent<RemoteBodyDriver>() == null;
	}

	[HarmonyPatch(typeof(Body), "Update")]
	internal static class BodyUpdatePatch
	{
		private static readonly MethodInfo HandleVisualsMethod =
			AccessTools.Method(typeof(Body), "HandleVisuals")
			?? throw new InvalidOperationException("Body.HandleVisuals not found.");

		private static bool Prefix(Body __instance)
		{
			if (__instance.GetComponentInParent<RemoteBodyDriver>() == null) // Unity object — ==
			{
				// Local player: while the start gate holds us, lock movement
				// (the game's own movingAllowed — Body.cs:4322) every frame;
				// the release restores it in GameAdapter.UpdateStartGate.
				if (PatchBridge.Impl is { IsWaitingForReady: true })
				{
					HarmonyLib.Traverse.Create(__instance).Field("movingAllowed").SetValue(false);
				}

				return true; // local player: original behavior
			}

			UpdateGrounded(__instance);
			UpdateCrouchAmount(__instance);
			// The 12s idle timer makes the original sit down (Body.cs:3162-3166);
			// a render proxy must stay in its standing pose — reset the timer.
			if (__instance.idleTime > 11f)
			{
				__instance.idleTime = 0f;
			}

			NeutralizePoseInputs(__instance);
			// Guard: the physics engine re-enables simulated on rigidbodies it
			// touches (observed: clone limb Rigidbody2D returning to simulated
			// within seconds of being frozen, then gravity pulling the limbs
			// apart). Re-freeze every frame — cheap and covers all paths.
			FreezeRigidbodies(__instance);
			HandleVisualsMethod.Invoke(__instance, [__instance.GetComponent<Painkillers>()]);

			// legSpeedMult is a computed property from leg force (Body.cs:67-95)
			// that is 0 on a proxy (limb force isn't simulated). HandleVisuals
			// feeds it into the CrouchAmount animator parameter
			// (Body.cs:3260: max(crouchAmount, 1 - legSpeedMult)), which pins the
			// state machine in the crouch clip (observed: ExperimentIdleCrouch).
			// Override: crouch pose is driven by the synced crouchAmount alone.
			var crouchParam = Body.InOutSine(Mathf.Clamp01(__instance.crouchAmount)) * 10000f;
			__instance.bodyAnimator.SetFloat("CrouchAmount", crouchParam);
			__instance.armsAnimator.SetFloat("CrouchAmount", crouchParam);

			// Climbing: currentClimbable is null on the proxy (a scene object
			// reference can't be synced), so HandleVisuals sets the climbing
			// animator flag to false every frame (Body.cs:3264-3265). Re-assert
			// it from the synced flag; climb velocity is fed via UpSpeed
			// (Body.cs:3256) from the synced rb.velocity.
			if (__instance.TryGetComponent<RemoteBodyDriver>(out var driver) && driver.Climbing)
			{
				__instance.bodyAnimator.SetBool("climbing", true);
				__instance.armsAnimator.SetBool("climbing", true);
			}

			return false;
		}

		private static void FreezeRigidbodies(Body body)
		{
			body.rb.simulated = false;
			foreach (var limb in body.limbs)
			{
				limb.rb.simulated = false;
			}
		}

		/// <summary>Ground probe mirroring Body.HandleGroundedState (Body.cs:2597), minus its side effects.</summary>
		private static void UpdateGrounded(Body body)
		{
			var probe = body.standing ? body.col.size : new Vector2(body.col.size.x, 0.25f);
			var distance = body.standing ? body.col.edgeRadius + 0.2f : 3.5f;
			var origin = (Vector2)body.transform.position + body.col.offset;
			body.grounded = Physics2D.BoxCast(
				origin, probe, 0f, Vector2.down, distance,
				LayerMask.GetMask("Ground")).collider != null; // Unity object — ==
		}

		/// <summary>Crouch pose easing identical to the local player's
		/// (Body.cs:3095-3099): the crouching flag flips instantly on the local
		/// body (HandlePhysics auto-crouch, Body.cs:3089) while crouchAmount
		/// ramps — a direct 0/1 write made the clone snap fully crouched while
		/// the owner was still visually standing.</summary>
		private static void UpdateCrouchAmount(Body body) => body.crouchAmount = Mathf.Lerp(body.crouchAmount, body.crouching ? 1f : 0f, Time.deltaTime * 6f);

		/// <summary>
		/// The proxy's pose must come from the skeleton alone. Every dynamic
		/// modifier HandleVisuals applies on top of the bone offset
		/// (Body.cs:3233-3252) depends on simulated state that is stale on a
		/// render clone:
		/// - accelRot is written only in FixedUpdate (Body.cs:2353) and Stand
		///   (Body.cs:1682, template leftover at Instantiate time);
		/// - standLerpTime/bodyLerpFromRagdoll come from the ragdoll-stand blend
		///   (Stand, Body.cs:1700): with standLerpTime starting at 0 the
		///   stand-blend lerp (Body.cs:3250) slowly morphs the clone from the
		///   template pose into a collapsed heap over tens of seconds
		///   (observed: limbs drifting downward until they pile up);
		/// - limb.bonusRot (head aim), attackRot, extraCrouchSmooth, armOffset
		///   are attack/aim leftovers.
		/// Zeroing them makes the modifiers no-ops. torsoLookSmooth is
		/// recomputed inside HandleVisuals and kept — it is the (reasonable)
		/// torso tracking of the synced aim point.
		/// </summary>
		private static void NeutralizePoseInputs(Body body)
		{
			foreach (var limb in body.limbs)
			{
				limb.bonusRot = 0f;
			}

			body.accelRot = 0f;
			body.attackRot = 0f;
			body.armOffset = 0f;
			// NOTE: legSpeedMult is a read-only property (Body.cs:67) — cannot
			// be reset here; it feeds the crouch animation parameter
			// (Body.cs:3260: max(crouchAmount, 1 - legSpeedMult)). A template
			// leftover below 1 would render the clone crouched while standing;
			// the crouchAmount easing below mitigates the visible part.
			// standLerpTime >= 1 also makes HandleVisuals clear bodyLerpFromRagdoll
			// (Body.cs:3221-3224) and extraCrouchSmooth lerps to 0 by itself
			// (Body.cs:3218) — both are private, no direct write needed.
			body.standLerpTime = 1f;
		}
	}

	[HarmonyPatch(typeof(Limb), "Update")]
	internal static class LimbUpdatePatch
	{
		// Limb.Update (Limb.cs:498+) simulates wounds/infection, writes shader
		// params from those numbers and consumes Random (Limb.cs:535) — none of
		// it applies to a render clone (its vitals are not synced). Skip.
		// GetComponentInParent: the driver lives on the Body GameObject.
		// == null: Unity object (a missing component is managed-null, same check).
		private static bool Prefix(Limb __instance) => __instance.GetComponentInParent<RemoteBodyDriver>() == null;
	}

	[HarmonyPatch(typeof(Body), "Attack")]
	internal static class BodyAttackPatch
	{
		// Player attacks damage building entities directly (Body.cs:1946 —
		// health -=, the only player-vs-entity damage write, no game event to
		// hook). Snapshot every entity's health before the attack; the postfix
		// reports whoever lost health. FindObjectsOfType per swing is fine —
		// attacks are low-frequency (1-2 Hz).
		private static void Prefix(out List<(BuildingEntity, float)> __state)
		{
			__state = [];
			foreach (var entity in UnityEngine.Object.FindObjectsOfType<BuildingEntity>())
			{
				__state.Add((entity, entity.health));
			}
		}

		private static void Postfix(List<(BuildingEntity, float)> __state)
		{
			foreach (var (entity, before) in __state)
			{
				if (entity != null && entity.health < before) // Unity object — ==
				{
					PatchBridge.Impl?.OnBuildingEntityDamaged(entity, before - entity.health);
				}
			}
		}
	}

	[HarmonyPatch(typeof(Body), "WearWearable")]
	internal static class WearWearablePatch
	{
		// An item went on a body part (mouth/hat/back…, Body.cs:1480) — the
		// peer's clone shows it only after a snapshot, so re-report right away
		// (the 1 Hz throttle alone reads as a delay on the peer's clone).
		// Wearing is ALSO the one item-left-the-world path that never reaches
		// PickUpItem: the touch auto-wear (Body.cs:527) and the radial-menu
		// drag (PlayerCamera.cs:1642) call WearWearable directly, so a world
		// item worn straight off the ground stayed in the world table and the
		// host's same-spot item was never removed ("worn — still on the
		// ground"; a late joiner could even pick up the ghost and wear it too
		// — one item id live in four places). The world-item verdict must be
		// captured in the PREFIX, before the wear re-parents the item (after
		// SetParent(limb) the IsWorldItem chain read false), together with the
		// ground position for the id-less generation-time binding.
		private static void Prefix(Item item, out bool __state)
		{
			__state = ItemWorldSync.IsWorldItem(item);
			if (__state)
			{
				PatchBridge.Impl?.OnItemPickupStart(item); // the ground position, before the wear re-parents the item
			}
		}

		private static void Postfix(Item item, bool __state)
		{
			PatchBridge.Impl?.OnInventoryChanged();
			// Report only a wear that actually landed: the item is parented to
			// a limb (Body.cs:1508). Failed paths (already worn, limb missing,
			// DoPickupCheck fail) never move the item — it stays a world item,
			// no report. The drop-out-of-slot → wear-in sequence is a
			// body-internal reorder and cancels inside the pickup sync (same
			// as a re-pick) — reusing OnItemPickedUp buys all of that.
			// A wear STRAIGHT FROM THE INVENTORY (hand/backpack → limb, the
			// radial menu's center drop) is a slot-move, not a pickup — it
			// reports through the carried-fact chain with the limb wear
			// encoding (OnItemWorn); the peer's clone re-homes it the moment
			// the wear lands instead of waiting for the character snapshot.
			if (item.transform.parent != null && item.transform.parent.GetComponent<Limb>() != null) // Unity objects — ==
			{
				if (__state)
				{
					PatchBridge.Impl?.OnItemPickedUp(item);
				}
				else
				{
					PatchBridge.Impl?.OnItemWorn(item);
				}
			}
		}
	}

	[HarmonyPatch(typeof(Body), "DropWearable")]
	internal static class DropWearablePatch
	{
		// An item came off a body part (Body.cs:1521) — the peer's clone
		// re-renders via the snapshot. The drop report (item domain entry +
		// instance id) is NOT duplicated here: the guarded Prefix in
		// BodyItemPatches already fires it into the merged drop report (one
		// drop = one report), and a second report here would materialize and
		// re-place the same item ("dropped — immediately desynced").
		private static void Postfix(Item item) => PatchBridge.Impl?.OnInventoryChanged();
	}

	[HarmonyPatch(typeof(Body), "Start")]
	internal static class BodyStartPatch
	{
		private static void Postfix(Body __instance)
		{
			Physics2D.IgnoreLayerCollision(
				LayerMask.GetMask("Player"), LayerMask.GetMask("Player"), true);

			var bodies = UnityEngine.Object.FindObjectsOfType<Body>();
			if (bodies.Length < 2)
			{
				return;
			}

			foreach (var other in bodies)
			{
				if (other == __instance)
				{
					continue;
				}

				var self = __instance.transform.parent?.GetComponentsInChildren<Collider2D>();
				var otherColliders = other.transform.parent?.GetComponentsInChildren<Collider2D>();
				if (self is null || otherColliders is null) // arrays — reference checks are fine
				{
					continue;
				}

				foreach (var a in self)
				{
					foreach (var b in otherColliders)
					{
						Physics2D.IgnoreCollision(a, b, true);
					}
				}
			}
		}
	}
}
