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
	/// <summary>
	/// Legacy 3-argument overload preserved for callers/tests that do not need
	/// the carry-proxy distinction; the local carried body is not a remote
	/// clone, so it must opt into the same visual-standing treatment explicitly.
	/// </summary>
	public static bool EffectiveVisualStanding(bool bodyStanding, bool isRemoteClone, bool hasExactLimbPose)
		=> EffectiveVisualStanding(bodyStanding, isRemoteClone, hasExactLimbPose, isCarryRenderProxy: false);

	/// <summary>
	/// Render-proxy pose rule. A frozen carry/piggyback local body has no
	/// simulation of its own, just like a remote clone: when the rider is
	/// conscious and alive, <c>HandleVisuals</c> must see a standing value even
	/// though <c>Body.standing</c> is held false by the ride-pose placement, so
	/// the animator continues to drive the visible limbs and matches the remote
	/// rider clone. Dead/unconscious carry remains non-standing (no fake
	/// standing for a corpse) unless exact owner limb poses are present.
	/// </summary>
	public static bool EffectiveVisualStanding(bool bodyStanding, bool isRemoteClone, bool hasExactLimbPose, bool isCarryRenderProxy)
		=> bodyStanding || (isCarryRenderProxy && !hasExactLimbPose) || (isRemoteClone && !hasExactLimbPose);
}
