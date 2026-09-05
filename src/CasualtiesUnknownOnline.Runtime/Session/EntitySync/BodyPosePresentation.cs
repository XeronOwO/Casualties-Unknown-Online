using System;

namespace CasualtiesUnknownOnline.Runtime.Session.EntitySync;

/// <summary>
/// Pure body-pose presentation rule for render proxies (no Unity): the local
/// body's CrouchAmount animator input is
/// <c>max(crouchAmount, 1 - legSpeedMult)</c> (Body.cs:3259). A remote clone
/// cannot read the owner's real <c>legSpeedMult</c> (it is a computed get-only
/// property over limb physics), so the 1 Hz character snapshot carries the
/// owner's value and this rule reconstructs the same visual input on the
/// frozen proxy. This is the systemic weakness/slouch pose path: severe
/// sleepiness, low consciousness/stamina, and other movement-debility states
/// all reduce <c>legSpeedMult</c> and must not appear as a straight standing
/// clone on the peer's view.
/// </summary>
public static class BodyPosePresentation
{
	/// <summary>
	/// The CrouchAmount animator input for a proxy. <paramref name="legSpeedMult"/>
	/// is the owner's reported value (0-1, clamped); <paramref name="crouchAmount"/>
	/// is the proxy's actual crouch amount. The result mirrors the rule the
	/// owner's own HandleVisuals applies.
	/// </summary>
	public static float ProxyCrouchInput(float crouchAmount, float legSpeedMult)
	{
		var strength = Math.Min(1f, Math.Max(0f, legSpeedMult));
		return Math.Max(crouchAmount, 1f - strength);
	}
}
