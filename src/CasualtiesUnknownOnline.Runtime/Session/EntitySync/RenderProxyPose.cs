namespace CasualtiesUnknownOnline.Runtime.Session.EntitySync;

/// <summary>
/// Pure render-proxy pose rule: a frozen remote clone has no physics to move
/// its visible limbs, so it normally must present as standing to
/// <c>Body.HandleVisuals</c> even when the synced <c>Body.standing</c> is false.
/// This lets the animator's LayDown/lying clip drive the visible limb
/// transforms. When the state stream carries an exact owner limb-pose fact, the
/// animator must be prevented from overwriting it (the proxy already has the
/// owner's real transforms written onto the visible limbs).
///
/// A LOCAL carried body is not a proxy and no longer takes this path: it keeps
/// its own native simulation and its own pose, with only its transform owned by
/// the carry relation (<see cref="CarriedBodySimulation"/>).
/// </summary>
public static class RenderProxyPose
{
	/// <summary>
	/// The standing value a frozen render proxy must present to
	/// <c>HandleVisuals</c> for this frame.
	/// </summary>
	public static bool EffectiveVisualStanding(bool bodyStanding, bool isRemoteClone, bool hasExactLimbPose)
		=> bodyStanding || (isRemoteClone && !hasExactLimbPose);
}
