namespace CasualtiesUnknownOnline.Runtime.Session.EntitySync;

/// <summary>
/// Pure render-proxy pose rule: a frozen remote clone has no physics to move
/// its visible limbs, so it must present as standing to
/// <c>Body.HandleVisuals</c> even when the synced <c>Body.standing</c> is false.
/// This lets the animator's LayDown/lying clip drive the visible limb
/// transforms. The semantic standing value is restored after the visual pass.
/// </summary>
public static class RenderProxyPose
{
	public static bool EffectiveVisualStanding(bool bodyStanding, bool isRemoteClone)
		=> bodyStanding || isRemoteClone;
}
