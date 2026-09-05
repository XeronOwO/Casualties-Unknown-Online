using System;
using System.Reflection;
using CasualtiesUnknownOnline.GameAdapter.Character;
using CasualtiesUnknownOnline.Runtime.Session.EntitySync;
using HarmonyLib;
using UnityEngine;

namespace CasualtiesUnknownOnline.GameAdapter.Patches;

/// <summary>
/// The <c>Body.Update</c> render-proxy and carry-participant presentation
/// patch. Local players keep the original simulation; remote clones and
/// carried/carrying bodies get the visual-only proxy path. This also holds the
/// native idle-sit suppression for every half of a carry relation.
/// </summary>
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
				Traverse.Create(__instance).Field("movingAllowed").SetValue(false);
			}

			// A LOCAL carrier must not start the native idle-sit: hold the
			// idle timer at zero before the original Body.Update runs so
			// HandleVisuals never sees the >12 s sit condition on the carry
			// side. The postfix below also exits an already-playing sit.
			if (IsLocalCarrier(__instance))
			{
				__instance.idleTime = 0f;
			}

			return true; // local player: original behavior
		}

		UpdateGrounded(__instance);
		UpdateCrouchAmount(__instance);
		// The 12s idle timer makes the original sit down (Body.cs:3162-3166);
		// a render proxy must stay in its standing pose — reset the timer.
		// A carry-participant body (local rider, carrier-side rider clone,
		// or any remote carrier clone) is held at zero every frame, not only
		// after the 11s pre-sit threshold, so the sit condition can never
		// begin accumulating on either half of the relation.
		var remoteDriver = __instance.GetComponent<RemoteBodyDriver>(); // Unity object — ==
		var isCarryParticipant = CarriedBodyDriver.IsCarrying(__instance)
			|| (remoteDriver != null && (remoteDriver.IsCarriedRider || remoteDriver.IsCarrier));
		if (CarriedBodyPose.ShouldZeroIdleTimer(isCarryParticipant) || __instance.idleTime > 11f)
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
		var originalStanding = __instance.standing;
		var isRemoteClone = __instance.GetComponentInParent<RemoteBodyDriver>() != null;
		var visualStanding = RenderProxyPose.EffectiveVisualStanding(
			originalStanding,
			isRemoteClone,
			remoteDriver != null && remoteDriver.RagdollPoseActive);
		if (!originalStanding && visualStanding)
		{
			__instance.standing = true;
		}

		try
		{
			HandleVisualsMethod.Invoke(__instance, [__instance.GetComponent<Painkillers>()]);
		}
		finally
		{
			__instance.standing = originalStanding;
		}

		// A carry-participant body that was already in the native sit clip
		// when the carry began must actively leave it: resetting idleTime
		// alone does not make HandleVisuals exit an already-playing
		// ExperimentSit clip when the proxy still presents as standing to
		// the animator.
		if (CarriedBodyPose.ShouldExitSit(isCarryParticipant, IsCurrentClipSit(__instance)))
		{
			__instance.bodyAnimator.Play("Grounded");
			__instance.armsAnimator.Play("Grounded");
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

	/// <summary>
	/// After the original Body.Update has run on a LOCAL carrier, actively
	/// leave an already-playing native sit clip. The prefix prevents the sit
	/// condition from starting, but a body that was already sitting when the
	/// carry began would otherwise keep the ExperimentSit/ArmsSit pair.
	/// </summary>
	private static void Postfix(Body __instance)
	{
		if (!IsLocalCarrier(__instance))
		{
			return;
		}

		if (CarriedBodyPose.ShouldExitSit(true, IsCurrentClipSit(__instance)))
		{
			__instance.bodyAnimator.Play("Grounded");
			__instance.armsAnimator.Play("Grounded");
		}
	}

	private static bool IsLocalCarrier(Body body) =>
		PatchBridge.Impl?.IsLocalCarrier(body) == true;

	private static void FreezeRigidbodies(Body body)
	{
		body.rb.simulated = false;
		foreach (var limb in body.limbs)
		{
			limb.rb.simulated = false;
		}
	}

	/// <summary>
	/// True when either the body or arms animator is currently on the native
	/// idle-sit clip (Body.cs:3152-3160 uses the same clip names to decide
	/// when to leave sit after the idle condition resets).
	/// </summary>
	private static bool IsCurrentClipSit(Body body)
	{
		var bodyClips = body.bodyAnimator.GetCurrentAnimatorClipInfo(0);
		if (bodyClips.Length != 0 && bodyClips[0].clip.name == "ExperimentSit")
		{
			return true;
		}

		var armClips = body.armsAnimator.GetCurrentAnimatorClipInfo(0);
		return armClips.Length != 0 && armClips[0].clip.name == "ArmsSit";
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
