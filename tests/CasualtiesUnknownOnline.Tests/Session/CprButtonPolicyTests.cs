using CasualtiesUnknownOnline.Runtime.Session.PlayerInteraction;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Session;

/// <summary>
/// The CPR control's three states (ticket `remote-medical-cpr.md`, stage 1c): a control appears only for a focused remote
/// body whose heart has stopped, becomes the compression control once this client's own CPR runs, and hides otherwise —
/// a disabled control still invites the viewer, which is the ruling the other read-only surfaces follow.
/// </summary>
[Trait("Category", "Unit")]
public class CprButtonPolicyTests
{
	[Fact]
	public void NoRemoteFocus_ShowsNothing() =>
		Assert.Equal(CprButtonState.Hidden, CprButtonPolicy.Decide(hasRemoteFocus: false, patientInArrest: true, ownCprRunning: true));

	[Fact]
	public void AStoppedHeart_StartsTheProcedure() =>
		Assert.Equal(CprButtonState.Start, CprButtonPolicy.Decide(hasRemoteFocus: true, patientInArrest: true, ownCprRunning: false));

	[Fact]
	public void ABeatingHeart_ShowsNothing() =>
		Assert.Equal(CprButtonState.Hidden, CprButtonPolicy.Decide(hasRemoteFocus: true, patientInArrest: false, ownCprRunning: false));

	[Fact]
	public void ARunningSession_IsTheCompressionControlEvenIfTheHeartRestarted() =>
		// The host arbitrates the restart; until the session ends the operator's control keeps compressing rather than
		// inviting a second start the host would refuse.
		Assert.Equal(CprButtonState.Compress, CprButtonPolicy.Decide(hasRemoteFocus: true, patientInArrest: false, ownCprRunning: true));

	[Fact]
	public void TheInterval_IsTheTimeBetweenPressesAndNeverZero()
	{
		Assert.Equal(0.55f, CprButtonPolicy.IntervalSeconds(previousPressSeconds: 10f, nowSeconds: 10.55f), 3);
		Assert.True(CprButtonPolicy.IntervalSeconds(previousPressSeconds: 10f, nowSeconds: 10f) > 0f);
	}
}
