using UnityEngine;

namespace CasualtiesUnknownOnline.GameAdapter.Character;

/// <summary>
/// Aim-angle math for the hand-held directional items on remote render clones.
/// The game points flashlight / emergencylight / rangefinder at the LOCAL
/// mouse from inside CustomItemBehaviour.Update (CustomItemBehaviour.cs:439,
/// 512, 526 — Camera.main.ScreenToWorldPoint(Input.mousePosition)); a render
/// clone must point them at the peer's 20 Hz reported aim instead, which
/// SessionStatePump already writes into Body.targetLookPos. The scalar shape
/// keeps the angle rule testable without constructing Unity value types in the
/// reflection-only test host.
/// </summary>
internal static class HeldItemDirection
{
	/// <summary>flashlight/emergencylight subtract 90 degrees from the aim angle (CustomItemBehaviour.cs:440, 527).</summary>
	internal const float LightAngleOffsetDegrees = -90f;

	/// <summary>rangefinder uses the aim angle as-is (CustomItemBehaviour.cs:513).</summary>
	internal const float SightAngleOffsetDegrees = 0f;

	/// <summary>
	/// The aim angle from (1,0) to (look - item) in degrees — the scalar
	/// equivalent of Vector2.SignedAngle(Vector2.right, (look - item).normalized).
	/// A zero-length aim has no direction: return 0, mirroring the game's
	/// normalized-zero-vector result.
	/// </summary>
	internal static float AimAngle(float itemX, float itemY, float lookX, float lookY)
	{
		var dx = lookX - itemX;
		var dy = lookY - itemY;
		if (dx * dx + dy * dy <= 1E-12f)
		{
			return 0f;
		}

		return Mathf.Atan2(dy, dx) * Mathf.Rad2Deg;
	}

	/// <summary>The final euler Z for one mouse-aimed item kind.</summary>
	internal static float AngleFor(float itemX, float itemY, float lookX, float lookY, float offsetDegrees) =>
		AimAngle(itemX, itemY, lookX, lookY) + offsetDegrees;
}
