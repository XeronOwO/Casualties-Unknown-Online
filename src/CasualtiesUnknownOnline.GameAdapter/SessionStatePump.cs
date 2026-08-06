using CasualtiesUnknownOnline.Runtime.Session;
using UnityEngine;

namespace CasualtiesUnknownOnline.GameAdapter;

/// <summary>
/// Applies a session entity's buffered state to a game Body. Used for render
/// proxies (guest's own body and the remote clone on the guest side) — the
/// body stops simulating locally and just reflects the host-authoritative state.
/// </summary>
internal static class SessionStatePump
{
	public static void Apply(PlayerEntity? entity, Body? body)
	{
		if (entity == null || body == null)
			return;

		body.transform.position = new Vector2(entity.Position.X, entity.Position.Y);
		body.targetLookPos = new Vector2(entity.LookPos.X, entity.LookPos.Y);
		body.rb.velocity = new Vector2(entity.Velocity.X, entity.Velocity.Y);
		body.isRight = entity.IsRight;
		body.standing = entity.Standing;
		// NOTE: Body.alive/conscious are get-only (private set) — Phase 1 render
		// proxies are always alive; death states land with health sync (Phase 3).
		body.crouching = entity.Crouching;
		body.moveDir = Vector2.zero; // never let local physics drive a render proxy
	}
}
