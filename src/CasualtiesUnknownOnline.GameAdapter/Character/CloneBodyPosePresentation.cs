using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using UnityEngine;

namespace CasualtiesUnknownOnline.GameAdapter.Character;

/// <summary>
/// The remote clone's body-pose presentation fields that must be captured from
/// the owner's simulated Body and replayed on the frozen render clone. The
/// current body-pose input is the owner's computed leg-speed multiplier
/// (<c>Body.legSpeedMult</c>), which drives the weakness/slouch portion of the
/// CrouchAmount animator parameter. A render clone cannot recompute it (its
/// limbs are frozen and <c>legSpeedMult</c> is a get-only property), so the 1 Hz
/// character snapshot carries it.
/// </summary>
internal static class CloneBodyPosePresentation
{
	internal static void Capture(Body body, CharacterHealthMsg health) =>
		health.LegSpeedMult = Mathf.Clamp01(body.legSpeedMult);

	internal static void Apply(Body clone, CharacterHealthMsg? health)
	{
		if (health is null)
		{
			return;
		}

		var driver = clone.GetComponent<RemoteBodyDriver>();
		if (driver != null) // Unity object — ==
		{
			driver.LegSpeedMult = Mathf.Clamp01(health.LegSpeedMult);
		}
	}
}
