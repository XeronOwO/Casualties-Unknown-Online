using UnityEngine;

namespace CasualtiesUnknownOnline.GameAdapter.Character;

/// <summary>
/// Shared facing presentation rule for CUO-driven bodies. The native game
/// renders facing through <c>transform.localScale.x</c> and keeps it in sync
/// with <c>Body.isRight</c> in <c>SwitchDir</c>; CUO path that only writes
/// <c>isRight</c> (carried local body, carrier-side clone override) must also
/// reconcile the scale or the released body is left with a logical facing and a
/// visual facing that disagree — the released body then cannot flip normally.
/// </summary>
internal static class BodyFacing
{
	/// <summary>
	/// The scale sign that matches a logical facing while preserving the
	/// sprite's current horizontal magnitude (a negative scale entered from a
	/// prior flip must become positive for isRight=false again, not stay
	/// negative).
	/// </summary>
	public static float FacingScale(bool isRight, float currentScaleX) =>
		Mathf.Abs(currentScaleX) * (isRight ? 1f : -1f);

	/// <summary>
	/// Writes the reconciled scale onto a Body. Unity object — callers pass a
	/// live Body and the method uses the Unity equality/access pattern through
	/// the Transform directly (no null check needed; caller guarantees it).
	/// </summary>
	public static void Apply(Body body)
	{
		var scale = body.transform.localScale;
		var targetX = FacingScale(body.isRight, scale.x);
		if (Mathf.Abs(scale.x - targetX) > 0.001f)
		{
			body.transform.localScale = new Vector3(targetX, scale.y, scale.z);
		}
	}
}
