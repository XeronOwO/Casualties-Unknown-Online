using System;
using CasualtiesUnknownOnline.Runtime.Protocol;
using CasualtiesUnknownOnline.Runtime.Session;
using UnityEngine;

namespace CasualtiesUnknownOnline.GameAdapter;

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
	private const float SnapshotInterval = 0.05f; // matches SessionService state send interval

	public static void Apply(PlayerEntity? entity, Body? body)
	{
		if (entity is null || body is null)
		{
			return;
		}

		// Interpolate toward the latest snapshot over one snapshot interval.
		var elapsed = (Environment.TickCount - entity.StateReceivedMs) / 1000f;
		var alpha = Mathf.Clamp01(elapsed / SnapshotInterval);
		var position = Vector2.Lerp(ToVector2(entity.PrevPosition), ToVector2(entity.Position), alpha);
		var lookPos = Vector2.Lerp(ToVector2(entity.PrevLookPos), ToVector2(entity.LookPos), alpha);
		var velocity = Vector2.Lerp(ToVector2(entity.PrevVelocity), ToVector2(entity.Velocity), alpha);

		body.transform.position = position;
		body.targetLookPos = lookPos;
		body.rb.velocity = velocity;
		body.isRight = entity.IsRight;
		body.standing = entity.Standing;
		// NOTE: Body.alive/conscious are get-only (private set) — Phase 1 render
		// proxies are always alive; death states land with health sync (Phase 3).
		body.crouching = entity.Crouching;
		body.moveDir = Vector2.zero; // never let local physics drive a render proxy
	}

	private static Vector2 ToVector2(NetVector2 v) => new(v.X, v.Y);
}
