namespace CasualtiesUnknownOnline.Runtime.Session.EntitySync;

/// <summary>
/// Pure render-proxy pose rule: a frozen remote clone has no physics to move
/// its visible limbs, so it normally must present as standing to
/// <c>Body.HandleVisuals</c> even when the synced <c>Body.standing</c> is false.
/// This lets the animator's LayDown/lying clip drive the visible limb
/// transforms. When the state stream carries an exact owner limb-pose fact,
/// the animator must be prevented from overwriting it (the proxy already has
/// the owner's real transforms written onto the visible limbs).
/// </summary>
public static class RenderProxyPose
{
	public static bool EffectiveVisualStanding(bool bodyStanding, bool isRemoteClone, bool hasExactLimbPose)
		=> bodyStanding || (isRemoteClone && !hasExactLimbPose);
}
