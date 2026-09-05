using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using CasualtiesUnknownOnline.Runtime.Session.CharacterData;
using UnityEngine;

namespace CasualtiesUnknownOnline.GameAdapter.Character;

/// <summary>
/// The remote clone's body-level facial-expression presentation: the
/// disfigurement and eye-loss latches that live on the owner's Body
/// (<c>disfigured</c>/<c>eyeGone</c>/<c>bothEyesGone</c>), the owner's random
/// disfigurement head index and long-run heal presentation timers on the
/// <see cref="FacialExpression"/> component, the face-driving body vitals
/// (consciousness, energy, bad-sleep, pain/sickness/radiation inputs) that the
/// game's own sprite formula reads, and the owner's actual
/// <see cref="HeadMouthState"/>. A render clone's <c>Body.Update</c> is
/// skipped, so the clone's <c>FacialExpression.Update</c> still runs but would
/// normally see template-default or proxy-local inputs; the vitals are written
/// from the 1 Hz <see cref="CharacterHealthMsg"/> and the head sprite is
/// restored to the owner's captured mouth state by
/// <see cref="Patches.FacialExpressionHeadPatch"/>.
/// </summary>
internal static class CloneFacePresentation
{
	/// <summary>Captures the owner-side face latches that Mapster cannot see:
	/// the random head index and the heal timers live on the
	/// <see cref="FacialExpression"/> child, not on <see cref="Body"/>.</summary>
	internal static void Capture(Body body, CharacterHealthMsg health)
	{
		// The owner's live mouth choice is the source of truth. The remote
		// clone independently derives the same sprite from clone-local
		// slot/limb/eat-time state, but those inputs are furnished by the
		// render-proxy path and have already diverged from the owner after
		// falls/injuries. Capture the game's own mouth decision here and let
		// the peer replay it instead of re-deriving it.
		health.EatTime = body.eatTime;
		health.HeadMouth = HeadMouthRule.Evaluate(
			body.disfigured,
			health.EatTime,
			body.HoldingItem(2),
			body.limbs.Length > 0 && body.limbs[0].dislocated);

		var face = body.GetComponentInChildren<FacialExpression>();
		if (face == null) // Unity object — ==
		{
			return;
		}

		health.DisfiguredIndex = face.disfiguredIndex;
		health.DisfiguredTimeFullSkin = face.disfiguredTimeFullSkin;
		health.EyeTimeHealed = face.eyeTimeHealed;
	}

	/// <summary>Applies the owner's face latches, face-driving vitals, and the
	/// owner's captured mouth state to a render clone. The Body booleans are
	/// what <c>FacialExpression.Update</c> reads directly; the child component
	/// fields keep the same disfigurement variant and heal-progress sprite
	/// choice as the owner; the vital fields feed the same sprite-selection
	/// branches the local body's simulated values would; the mouth state is
	/// stashed on <see cref="RemoteBodyDriver"/> for the head-sprite postfix.
	/// The pure field projection is <see cref="FacePresentationVitals"/>.</summary>
	internal static void Apply(Body clone, CharacterHealthMsg? health)
	{
		if (health is null)
		{
			return;
		}

		clone.disfigured = health.Disfigured;
		clone.eyeGone = health.EyeGone;
		clone.bothEyesGone = health.BothEyesGone;

		// Face-driving body vitals: Body.Update is skipped on a render clone,
		// so these are written from the owner's 1 Hz snapshot. The game's own
		// FacialExpression.Update remains the sprite authority.
		ApplyVitals(clone, FacePresentationVitals.From(health));

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

	private static void ApplyVitals(Body clone, FacePresentationVitals vitals)
	{
		clone.consciousness = vitals.Consciousness;
		clone.energy = vitals.Energy;
		clone.badSleepAmount = vitals.BadSleepAmount;
		clone.radiationSickness = vitals.RadiationSickness;
		clone.shock = vitals.Shock;
		clone.adrenaline = vitals.Adrenaline;
		clone.sicknessAmount = vitals.SicknessAmount;
		clone.temperature = vitals.Temperature;
		clone.internalBleeding = vitals.InternalBleeding;
		clone.bloodPressure = vitals.BloodPressure;
		clone.happiness = vitals.Happiness;

		// Stash the owner's mouth choice on the remote-managed body so the
		// FacialExpression postfix can restore the exact owner head sprite
		// after the game's formula has run on the frozen clone.
		var driver = clone.GetComponent<RemoteBodyDriver>();
		if (driver != null) // Unity object — ==
		{
			driver.HeadMouth = vitals.HeadMouth;
		}
	}
}
