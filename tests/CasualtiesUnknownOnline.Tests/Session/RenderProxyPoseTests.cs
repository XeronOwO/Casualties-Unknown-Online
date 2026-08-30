using CasualtiesUnknownOnline.Runtime.Session.EntitySync;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Session;

/// <summary>
/// The pure render-proxy pose rule behind the remote ragdoll fix: a frozen
/// remote clone must present as standing during Body.HandleVisuals even when
/// the synced Body.standing is false, so the LayDown/lying clip can move the
/// visible limb transforms.
/// </summary>
public class RenderProxyPoseTests
{
	[Fact]
	public void RemoteCloneNotStanding_PresentsStandingForVisuals()
	{
		Assert.True(RenderProxyPose.EffectiveVisualStanding(
			bodyStanding: false,
			isRemoteClone: true),
			"a lying remote clone must render as standing to HandleVisuals so the animator drives its limbs");
	}

	[Fact]
	public void NonRemoteNotStanding_DoesNotPresentStandingForVisuals()
	{
		Assert.False(RenderProxyPose.EffectiveVisualStanding(
			bodyStanding: false,
			isRemoteClone: false),
			"a non-remote non-standing body must keep its semantic standing value");
	}

	[Fact]
	public void StandingBody_AlwaysPresentsStanding()
	{
		Assert.True(RenderProxyPose.EffectiveVisualStanding(
			bodyStanding: true,
			isRemoteClone: false));
		Assert.True(RenderProxyPose.EffectiveVisualStanding(
			bodyStanding: true,
			isRemoteClone: true));
	}
}
