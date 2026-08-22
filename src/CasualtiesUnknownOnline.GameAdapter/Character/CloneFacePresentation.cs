using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using UnityEngine;

namespace CasualtiesUnknownOnline.GameAdapter.Character;

/// <summary>
/// The remote clone's body-level facial-expression presentation: the
/// disfigurement and eye-loss latches that live on the owner's Body
/// (<c>disfigured</c>/<c>eyeGone</c>/<c>bothEyesGone</c>) plus the owner's
/// random disfigurement head index and the long-run heal presentation timers
/// on the <see cref="FacialExpression"/> component. The clone's own
/// <c>FacialExpression.Update</c> still runs (it is not a simulated body
/// patch), so these fields are written from the 1 Hz
/// <see cref="CharacterHealthMsg"/> and the face sprite then follows the game's
/// own visual rules.
/// </summary>
internal static class CloneFacePresentation
{
	/// <summary>Captures the owner-side face latches that Mapster cannot see:
	/// the random head index and the heal timers live on the
	/// <see cref="FacialExpression"/> child, not on <see cref="Body"/>.</summary>
	internal static void Capture(Body body, CharacterHealthMsg health)
	{
		var face = body.GetComponentInChildren<FacialExpression>();
		if (face == null) // Unity object — ==
		{
			return;
		}

		health.DisfiguredIndex = face.disfiguredIndex;
		health.DisfiguredTimeFullSkin = face.disfiguredTimeFullSkin;
		health.EyeTimeHealed = face.eyeTimeHealed;
	}

	/// <summary>Applies the owner's face latches to a render clone. The three
	/// Body booleans are what <c>FacialExpression.Update</c> reads; the child
	/// component fields keep the same disfigurement variant and heal-progress
	/// sprite choice as the owner.</summary>
	internal static void Apply(Body clone, CharacterHealthMsg? health)
	{
		if (health is null)
		{
			return;
		}

		clone.disfigured = health.Disfigured;
		clone.eyeGone = health.EyeGone;
		clone.bothEyesGone = health.BothEyesGone;

		var face = clone.GetComponentInChildren<FacialExpression>();
		if (face == null) // Unity object — ==
		{
			return;
		}

		// The owner's index is always in range; a malformed/old wire value must
		// never index outside the sprite array on the clone.
		var count = face.disfiguredHead?.Length ?? 0;
		face.disfiguredIndex = count > 0 ? Mathf.Clamp(health.DisfiguredIndex, 0, count - 1) : 0;
		face.disfiguredTimeFullSkin = health.DisfiguredTimeFullSkin;
		face.eyeTimeHealed = health.EyeTimeHealed;
	}
}
