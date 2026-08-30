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
		// A carried local body is also proxy-driven: its simulation is skipped
		// and GameAdapter.CarryInteraction moves its transform instead.
		private static bool Prefix(Body __instance) =>
			__instance.GetComponentInParent<RemoteBodyDriver>() == null
			&& !CarriedBodyDriver.IsCarrying(__instance);
	}

	[HarmonyPatch(typeof(Body), "Update")]
	internal static class BodyUpdatePatch
	{
		private static readonly MethodInfo HandleVisualsMethod =
			AccessTools.Method(typeof(Body), "HandleVisuals")
			?? throw new InvalidOperationException("Body.HandleVisuals not found.");

		private static bool Prefix(Body __instance)
		{
			if (__instance.GetComponentInParent<RemoteBodyDriver>() == null
				&& !CarriedBodyDriver.IsCarrying(__instance)) // Unity objects — ==
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
			// Wall-slide: the owner's HandleGroundedState sets private
			// slidingLeft/Right from move input + side raycasts; write the
			// synced flags onto the clone before HandleVisuals plays the Wall
			// clip/params, and mirror the continuous particle/audio latch.
			{
				var wallDriver = __instance.GetComponent<RemoteBodyDriver>();
				if (wallDriver != null) // Unity object — ==
				{
					WallSlidePresentation.Apply(__instance, wallDriver.SlidingLeft, wallDriver.SlidingRight);
					WallSlidePresentation.UpdateEffects(__instance, wallDriver.SlidingLeft, wallDriver.SlidingRight);
				}
			}
			// A frozen remote clone cannot physics-drive the visible limbs
			// during a ragdoll/death/unconscious pose (Body.standing=false skips
			// the animLimb -> limb copy inside HandleVisuals, Body.cs:3224-3252).
			// Temporarily present the remote clone as standing to HandleVisuals
			// so the LayDown/lying clip still drives the limb transforms; the
			// synced standing value is restored immediately after the visual
			// pass and remains the semantic state for SessionStatePump/LyingPose.
			var visualStanding = __instance.standing;
			if (!visualStanding && __instance.GetComponentInParent<RemoteBodyDriver>() != null)
			{
				__instance.standing = true;
			}

			try
			{
				HandleVisualsMethod.Invoke(__instance, [__instance.GetComponent<Painkillers>()]);
			}
			finally
			{
				__instance.standing = visualStanding;
			}

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
		// A carried local body is likewise proxy-driven while carried.
		private static bool Prefix(Limb __instance) =>
			__instance.GetComponentInParent<RemoteBodyDriver>() == null
			&& !CarriedBodyDriver.IsCarryingInParent(__instance);
	}

	[HarmonyPatch(typeof(Body), "Attack")]
	internal static class BodyAttackPatch
	{
		// Per-call state: whether the swing runs (the original's guard, Body.cs:
		// 1843 + 1885 — conscious, off-cooldown, and doAttackAnim) and every
		// entity's pre-attack health. Player attacks damage building entities
		// directly (Body.cs:1946 — health -=, the only player-vs-entity damage
		// write, no game event to hook); the postfix reports whoever lost health.
		// FindObjectsOfType per swing is fine — attacks are low-frequency (1-2 Hz).
		private sealed class AttackState
		{
			internal bool WillRun;
			internal bool IsLocalBody;
			internal string? AttackAnim;
			internal List<(BuildingEntity, float)> Entities = [];
			internal IDisposable? SoundScope;
		}

		private static void Prefix(Body __instance, AttackInfo atk, out AttackState __state)
		{
			// A postfix can't tell a real swing from a no-op: attackCooldown is
			// also > 0 for a no-op while a previous cooldown is still active.
			// Capture the exact guard the original checks (Body.cs:1843, 1885).
			// The character-sound capture scope: string Sound.Play calls that
			// run inside the native attack are the swing/exert sounds. A render
			// clone never attacks — no scope, no capture (its Body.Update is
			// already skipped, this is the belt-and-braces guard).
			var isLocalBody = __instance.GetComponentInParent<RemoteBodyDriver>() == null
				&& !CarriedBodyDriver.IsCarrying(__instance); // Unity objects — ==
			__state = new AttackState
			{
				WillRun = __instance.conscious && __instance.attackCooldown <= 0f && atk.doAttackAnim,
				IsLocalBody = isLocalBody,
				AttackAnim = atk.attackAnim != null ? atk.attackAnim.name : null, // Unity object — ==
				SoundScope = isLocalBody
					? CallContext.Enter(CallContext.Origin.CharacterAttack)
					: null,
			};

			foreach (var entity in UnityEngine.Object.FindObjectsOfType<BuildingEntity>())
			{
				__state.Entities.Add((entity, entity.health));
			}
		}

		private static void Postfix(Body __instance, AttackState __state)
		{
			__state.SoundScope?.Dispose();
			if (__state.WillRun)
			{
				PatchBridge.Impl?.OnArmSwing();
				if (__state.IsLocalBody && __state.AttackAnim is { } prefab)
				{
					// The original computes the swing vector after the guard
					// (Body.cs:1854) and instantiates the anim after the sound
					// (Body.cs:1913). Recompute the same facts in the postfix:
					// isRight/targetLookPos/arm are the final values the
					// original used for the visual.
					var direction = ((Vector2)__instance.targetLookPos - (Vector2)__instance.limbs[1].transform.position).normalized;
					PatchBridge.Impl?.OnAttackAnim(prefab, direction, __instance.isRight, __instance.limbs[1].transform.position);
				}
			}

			foreach (var (entity, before) in __state.Entities)
			{
				if (entity != null && entity.health < before) // Unity object — ==
				{
					PatchBridge.Impl?.OnBuildingEntityDamaged(entity, before - entity.health, playHitSound: true);
				}
			}
		}
	}

	/// <summary>
	/// Body.TryExertSound (Body.cs:2103-2109) plays one of the four exert clips
	/// when its Random gate passes — the call-identity scope around it lets the
	/// Sound.Play patch report the exact chosen clip. Called directly by Attack/
	/// Jump/other body actions and patched once here instead of at every call
	/// site (whole-family coverage).
	/// </summary>
	[HarmonyPatch(typeof(Body), "TryExertSound")]
	internal static class BodyTryExertSoundPatch
	{
		private static void Prefix(Body __instance, out IDisposable? __state) =>
			__state = __instance.GetComponentInParent<RemoteBodyDriver>() == null
				? CallContext.Enter(CallContext.Origin.CharacterExert)
				: null;

		private static void Postfix(IDisposable? __state) => __state?.Dispose();
	}

	/// <summary>
	/// Body.FootStep (Body.cs:1169-1184) is the single entry point for every
	/// player step sound: animation events, jump/walljump take-off, and the
	/// landing roll all call it. On the local body (never a render clone) it
	/// opens the CharacterFootstep scope so the string/AudioClip Sound.Play
	/// patches capture the exact step clip. The step-surface prefix is stored
	/// for the AudioClip overload: material/water steps are RandomStepSound
	/// clips under Sounds/footstep/&lt;step&gt;/ and need the prefix to make a
	/// loadable string resource.
	/// </summary>
	[HarmonyPatch(typeof(Body), "FootStep")]
	internal static class BodyFootStepPatch
	{
		private sealed class FootstepState
		{
			internal IDisposable? Scope;
			internal string? StepPathPrefix;
		}

		private static void Prefix(Body __instance, out FootstepState __state)
		{
			FootstepSoundCapture.ClearStepPathPrefix();
			if (__instance.GetComponentInParent<RemoteBodyDriver>() != null)
			{
				__state = new FootstepState();
				return;
			}

			var standingOn = Traverse.Create(__instance).Field("standingOn");
			var prefix = __instance.bodyAffect.wasWater
				? "footstep/Water"
				: standingOn.FieldExists() && standingOn.GetValue() is BlockInfo blockInfo
					? "footstep/" + blockInfo.stepsound
					: null;
			FootstepSoundCapture.SetStepPathPrefix(prefix);
			__state = new FootstepState
			{
				Scope = CallContext.Enter(CallContext.Origin.CharacterFootstep),
				StepPathPrefix = prefix,
			};
		}

		private static void Postfix(FootstepState __state)
		{
			__state.Scope?.Dispose();
			FootstepSoundCapture.ClearStepPathPrefix();
		}
	}

	/// <summary>
	/// Body.HandleGroundedState (Body.cs:2594) is the landing-impact entry:
	/// on the local body it opens the CharacterLandingImpact scope around the
	/// frame, so the impactLarge/Medium/Small AudioClip calls (Body.cs:2729-2737)
	/// report as landing impacts. The nested FootStep call inside the same
	/// method runs in the innermost CharacterFootstep scope and reports as a
	/// footstep. The postfix additionally reports the landing presentation
	/// (the Grounded clip + optional DustSmall/DustBig) as a dedicated one-shot
	/// event for the peers' render clones.
	/// </summary>
	[HarmonyPatch(typeof(Body), "HandleGroundedState")]
	internal static class BodyHandleGroundedStatePatch
	{
		private sealed class LandingState
		{
			internal IDisposable? Scope;
			internal bool IsLocalBody;
			internal bool WasGrounded;
		}

		private static void Prefix(Body __instance, out LandingState __state)
		{
			__state = new LandingState
			{
				IsLocalBody = __instance.GetComponentInParent<RemoteBodyDriver>() == null
					&& !CarriedBodyDriver.IsCarrying(__instance), // Unity objects — ==
				WasGrounded = __instance.grounded,
			};
			if (__state.IsLocalBody)
			{
				__state.Scope = CallContext.Enter(CallContext.Origin.CharacterLandingImpact);
			}
		}

		private static void Postfix(Body __instance, LandingState __state)
		{
			__state.Scope?.Dispose();
			if (!__state.IsLocalBody || __state.WasGrounded || !__instance.grounded)
			{
				return;
			}

			var cloudSize = LandingCloudSize(__instance);
			if (cloudSize != 0)
			{
				var originalSize = Traverse.Create(__instance).Field("origColSize").GetValue<Vector2>();
				var position = (Vector2)__instance.transform.position + Vector2.down * (originalSize.y * 0.5f);
				PatchBridge.Impl?.OnCharacterLandingVisual(cloudSize, position, __instance.rb.velocity.x);
			}
			else
			{
				// A soft landing still replays the Grounded clip on the clone,
				// even though no dust was spawned.
				PatchBridge.Impl?.OnCharacterLandingVisual(0, Vector2.zero, 0f);
			}
		}

		private static byte LandingCloudSize(Body body)
		{
			var impact = body.lastTimeStepVelocity.y;
			if (impact >= -body.jumpSpeed * 0.35f)
			{
				return 0;
			}

			return impact < -body.jumpSpeed - 5f
				? CasualtiesUnknownOnline.Runtime.Protocol.Messages.CharacterLandingVisualMsg.CloudBig
				: CasualtiesUnknownOnline.Runtime.Protocol.Messages.CharacterLandingVisualMsg.CloudSmall;
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
			// A wearable craft product wears during the craft — its fact rides
			// the ONE craft report (OnInventoryChanged still fires above: the
			// character snapshot baseline must re-report).
			if (CallContext.Current != CallContext.Origin.Craft
				&& item.transform.parent != null && item.transform.parent.GetComponent<Limb>() != null) // Unity objects — ==
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
