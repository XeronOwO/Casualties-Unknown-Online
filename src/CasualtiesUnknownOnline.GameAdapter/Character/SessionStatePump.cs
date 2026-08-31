using System;
using CasualtiesUnknownOnline.Runtime.Protocol;
using CasualtiesUnknownOnline.Runtime.Session.EntitySync;
using UnityEngine;

namespace CasualtiesUnknownOnline.GameAdapter.Character;

/// <summary>
/// Applies a session entity's buffered state to a game Body. Used for render
/// proxies (the remote player's clone on either side) — the body's physics is
/// frozen (FixedUpdate skipped, Rigidbody2D non-simulated) and it just reflects
/// the peer's reported state. Body.Update still runs so visuals and animations
/// (walk/jump poses from the synced velocity) keep working.
///
/// Position/look/velocity are interpolated between the last two snapshots
/// (20 Hz source) so the proxy moves smoothly instead of stepping; discrete
/// flags apply immediately. This is render smoothing, not client prediction
/// (architecture.md §12 keeps prediction out of the MVP).
/// </summary>
internal static class SessionStatePump
{
	public static void Apply(PlayerEntity? entity, Body? body)
	{
		// NOTE: body is a Unity object — use == (operator overload) not is null;
		// a scene reload destroys the clone and reference-comparison misses it.
		if (entity is null || body == null)
		{
			return;
		}

		// No snapshot yet (StateReceivedMs stays int.MinValue until the first
		// report arrives): keep the clone at its spawn anchor. Applying now
		// would snap it to the buffer's default position (0,0).
		if (entity.StateReceivedMs < 0)
		{
			return;
		}

		// Interpolate toward the latest snapshot over the EMA-averaged snapshot
		// arrival interval: a fixed window overshoots and waits when the
		// sender's throttle degrades (a low-frame-rate guest reports at uneven
		// 33/66 ms intervals) — stepping/jerk on the proxy; the RAW per-snapshot
		// interval jitters on an unreliable channel (a delayed tick doubles the
		// window, then the next tick halves it — the proxy speeds up and stalls,
		// reads as "stuttering" while the local game stays smooth). The average
		// reaches the target smoothly and absorbs single-tick jitter.
		body.TryGetComponent<RemoteBodyDriver>(out var driver); // == null: a missing component is managed-null — same check
		var elapsed = (Environment.TickCount - entity.StateReceivedMs) / 1000f;
		if (driver != null && entity.StateReceivedMs != driver.LastStateMs)
		{
			if (driver.LastStateMs > 0)
			{
				var interval = Mathf.Clamp((entity.StateReceivedMs - driver.LastStateMs) / 1000f, 0.02f, 0.5f);
				driver.AvgIntervalSec = driver.AvgIntervalSec <= 0f ? interval : driver.AvgIntervalSec * 0.8f + interval * 0.2f;
			}

			driver.LastStateMs = entity.StateReceivedMs;
		}

		var window = driver != null && driver.AvgIntervalSec > 0f ? driver.AvgIntervalSec : 0.05f;
		var alpha = Mathf.Clamp01(elapsed / window);
		var position = Vector2.Lerp(ToVector2(entity.PrevPosition), ToVector2(entity.Position), alpha);
		var lookPos = Vector2.Lerp(ToVector2(entity.PrevLookPos), ToVector2(entity.LookPos), alpha);
		var velocity = Vector2.Lerp(ToVector2(entity.PrevVelocity), ToVector2(entity.Velocity), alpha);

		body.transform.position = position;
		body.targetLookPos = lookPos;
		// LookTarget/CorpseScript override gaze: write the owner's override
		// target and remaining timers onto the proxy. Body.Update is skipped on
		// the proxy, so these are refreshed from the 20 Hz stream rather than
		// decayed locally; HandleVisuals reads overrideLookTime > 0 to turn the
		// head/eyes toward the override point (Body.cs:3178) and
		// FacialExpression reads eyeScareTime for the scared face.
		if (entity.LookOverridePos is { } overridePos)
		{
			body.overrideLookPos = new Vector2(overridePos.X, overridePos.Y);
		}

		body.overrideLookTime = entity.LookOverrideTime;
		body.eyeScareTime = entity.EyeScareTime;
		body.eyePanicTime = entity.EyePanicTime;
		body.eyeCloseTime = entity.EyeCloseTime;
		body.rb.velocity = velocity;
		body.isRight = entity.IsRight;
		// Facing is RENDERED via transform.localScale.x (SwitchDir, Body.cs:1187-
		// 1209). On a proxy the auto-flip in HandleVisuals (Body.cs:3131) never
		// triggers (moveDir=0, attackCooldown=0) — mirror the scale sign here.
		BodyFacing.Apply(body);

		// The ragdoll one-shot is reliable; the 20 Hz standing flag is not. A
		// collapse event may arrive before the state stream's standing=false
		// snapshot, and an older standing=true snapshot can then overwrite the
		// replay. The gate keeps the proxy lying for the short suppression
		// window until the stream confirms the collapse (or the window expires).
		var effectiveStanding = entity.Standing;
		if (driver != null)
		{
			if (entity.Standing)
			{
				var suppressStanding = RagdollPoseGate.ShouldSuppressStanding(
					entity.Standing,
					driver.RagdollCollapsePending,
					driver.RagdollCollapseConfirmed,
					driver.RagdollCollapseMs,
					Environment.TickCount);
				if (suppressStanding)
				{
					effectiveStanding = false;
				}
				else
				{
					// The collapse is either confirmed or the suppression window
					// expired: a standing=true state is now a real stand-up.
					driver.RagdollCollapsePending = false;
					driver.RagdollCollapseConfirmed = false;
				}
			}
			else
			{
				driver.RagdollCollapseConfirmed = true;
			}
		}

		body.standing = effectiveStanding;
		// Exact owner limb-pose facts (ragdoll/dead/unconscious) beat the
		// animator's approximate LayDown clip on the frozen proxy. When the
		// stream stops carrying poses (stand-up/sleeping), clear the override and
		// let HandleVisuals drive the clone again.
		RagdollPoseApplication.Apply(body, entity.LimbPoses, driver);
		// Body.alive/conscious are derived properties (brainHealth > 0, Body.cs:203)
		// — the proxy's own simulation would keep them consistent locally, but we
		// render death explicitly: alive=false forces the lying pose immediately
		// (the peer's standing flag lags one snapshot behind its Ragdoll, so the
		// proxy would otherwise stand "dead" for a few frames). The game itself has
		// no respawn — death ends the run (menu → new run is the scene-switch
		// flow, SessionStatePump needs nothing extra for it).
		body.crouching = entity.Crouching;
		body.sleeping = entity.Sleeping;
		body.moveDir = Vector2.zero; // never let local physics drive a render proxy

		// Pose clips play only on transitions — the state machine picks the
		// walk/stand clips back up via HandleVisuals once the peer changes pose
		// (idle timer resets on movement, NapCoroutine plays the lay clips).
		if (driver != null)
		{
			if (entity.Sitting != driver.PrevSitting)
			{
				driver.PrevSitting = entity.Sitting;
				if (entity.Sitting)
				{
					body.bodyAnimator.Play("ExperimentSit");
					body.armsAnimator.Play("ArmsSit");
				}
			}

			if (entity.Sleeping != driver.PrevSleeping
				|| (entity.Sleeping && entity.NapVariant != driver.PrevNapVariant))
			{
				driver.PrevSleeping = entity.Sleeping;
				driver.PrevNapVariant = entity.NapVariant;
				if (entity.Sleeping)
				{
					body.bodyAnimator.Play(NapPresentation.BodyClip(entity.NapVariant));
					body.armsAnimator.Play(NapPresentation.ArmsClip(entity.NapVariant));
				}
			}
			else if (!entity.Sleeping)
			{
				driver.PrevNapVariant = entity.NapVariant;
			}

			// Dog-shake is a continuous presentation fact (Body.dogShakeIntensity
			// is public and HandleVisuals reads it on the proxy) — write the
			// synced value so the clone shakes/calms with the owner.
			body.dogShakeIntensity = entity.DogShakeIntensity;

			// Wall-slide presentation is a continuous fact: cache the wire
			// flags on the driver so BodyPatches can re-assert the private
			// Body.sliding* fields and drive the wall particle every frame.
			driver.SlidingLeft = entity.SlidingLeft;
			driver.SlidingRight = entity.SlidingRight;

			// Lying (ragdoll/dead/unconscious — !standing without sleeping, or
			// !alive): the LayDown clip approximates the ragdoll pose on the
			// proxy (real ragdoll is physics-driven, frozen here by design).
			// The rule is the pure LyingPose machine (L0-locked).
			var lying = LyingPose.IsLying(effectiveStanding, entity.Alive, entity.Sleeping);
			if (lying != driver.PrevLying)
			{
				driver.PrevLying = lying;
				if (lying)
				{
					body.bodyAnimator.Play("ExperimentLayDown");
					body.armsAnimator.Play("ArmsLayDown");
				}
			}

			driver.Climbing = entity.Climbing;

			// Workout/exercise: the owner's DoWorkout plays a specific
			// animator clip set; replay it on the proxy when the wire type
			// changes. The value is refreshed every 20 Hz, so a lost packet is
			// self-healed; returning to 0 restores the standing clips.
			if (entity.WorkoutType != driver.PrevWorkoutType)
			{
				driver.PrevWorkoutType = entity.WorkoutType;
				ReplayWorkout(body, entity.WorkoutType);
			}

			// Attack swing: replay the ArmsSwing clip once per swing — the
			// SwingReplay machine replays on every sequence CHANGE (each swing,
			// even several inside one held flag window — rapid mining swings)
			// with the flag's rising edge as the old-sender fallback (a peer
			// that never sends the sequence). The IsAttacking flag is held for
			// the swing's span by the sender (AttackSwingState), so the edge
			// is delivered reliably even over the unreliable 20 Hz stream.
			if (SwingReplay.ShouldReplay(entity.SwingSeq, driver.PrevSwingSeq,
				entity.IsAttacking, driver.PrevAttacking, driver.SwingStateSeeded))
			{
				body.armsAnimator.Play("ArmsSwing", -1, 0f);
			}

			driver.PrevSwingSeq = entity.SwingSeq;
			driver.PrevAttacking = entity.IsAttacking;
			driver.SwingStateSeeded = true;
		}
	}

	private static void ReplayWorkout(Body body, byte workoutType)
	{
		if (!WorkoutPresentation.IsWorkout(workoutType))
		{
			body.bodyAnimator.SetBool("exercising", false);
			body.bodyAnimator.Play("Grounded");
			body.armsAnimator.Play("Grounded");
			return;
		}

		body.bodyAnimator.SetBool("exercising", true);
		body.bodyAnimator.Play(WorkoutPresentation.BodyClip(workoutType));
		body.armsAnimator.Play(WorkoutPresentation.ArmsClip(workoutType));
	}

	private static Vector2 ToVector2(NetVector2 v) => new(v.X, v.Y);
}
