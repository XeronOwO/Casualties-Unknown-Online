using System;
using CasualtiesUnknownOnline.Runtime.Protocol;
using CasualtiesUnknownOnline.Runtime.Session.EntitySync;
using UnityEngine;

namespace CasualtiesUnknownOnline.GameAdapter.Rendering;

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
		body.rb.velocity = velocity;
		body.isRight = entity.IsRight;
		// Facing is RENDERED via transform.localScale.x (SwitchDir, Body.cs:1187-
		// 1209). On a proxy the auto-flip in HandleVisuals (Body.cs:3131) never
		// triggers (moveDir=0, attackCooldown=0) — mirror the scale sign here.
		var scale = body.transform.localScale;
		var targetX = Mathf.Abs(scale.x) * (entity.IsRight ? 1f : -1f);
		if (Mathf.Abs(scale.x - targetX) > 0.001f)
		{
			body.transform.localScale = new Vector3(targetX, scale.y, scale.z);
		}

		body.standing = entity.Standing;
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

			if (entity.Sleeping != driver.PrevSleeping)
			{
				driver.PrevSleeping = entity.Sleeping;
				if (entity.Sleeping)
				{
					body.bodyAnimator.Play("ExperimentLayDown");
					body.armsAnimator.Play("ArmsLayDown");
				}
			}

			// Lying (ragdoll/dead/unconscious — !standing without sleeping, or
			// !alive): the LayDown clip approximates the ragdoll pose on the
			// proxy (real ragdoll is physics-driven, frozen here by design).
			var lying = (!entity.Standing || !entity.Alive) && !entity.Sleeping;
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
		}
	}

	private static Vector2 ToVector2(NetVector2 v) => new(v.X, v.Y);
}
