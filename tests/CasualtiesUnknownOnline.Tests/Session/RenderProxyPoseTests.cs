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
			isRemoteClone: true,
			hasExactLimbPose: false),
			"a lying remote clone without exact pose data must render as standing to HandleVisuals so the animator drives its limbs");
	}

	[Fact]
	public void RemoteCloneWithExactLimbPose_DoesNotPresentStandingForVisuals()
	{
		Assert.False(RenderProxyPose.EffectiveVisualStanding(
			bodyStanding: false,
			isRemoteClone: true,
			hasExactLimbPose: true),
			"a lying remote clone with exact owner limb poses must not let HandleVisuals overwrite them with the animator skeleton");
	}

	[Fact]
	public void NonRemoteNotStanding_DoesNotPresentStandingForVisuals()
	{
		Assert.False(RenderProxyPose.EffectiveVisualStanding(
			bodyStanding: false,
			isRemoteClone: false,
			hasExactLimbPose: false),
			"a non-remote non-standing body must keep its semantic standing value");
	}

	[Fact]
	public void StandingBody_AlwaysPresentsStanding()
	{
		Assert.True(RenderProxyPose.EffectiveVisualStanding(
			bodyStanding: true,
			isRemoteClone: false,
			hasExactLimbPose: false));
		Assert.True(RenderProxyPose.EffectiveVisualStanding(
			bodyStanding: true,
			isRemoteClone: true,
			hasExactLimbPose: true));
	}
}
