using CasualtiesUnknownOnline.Runtime.Session.PlayerInteraction;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Session;

/// <summary>
/// The AED's advice rule (ticket `remote-medical-cpr.md`, stage 3's second half): a shock is only delivered to a
/// fibrillating heart, so whichever procedure runs second against the patient's own snapshot finds its condition gone and
/// refuses — the arbitration the ruling asks for, without either procedure knowing about the other.
/// </summary>
[Trait("Category", "Unit")]
public class AedShockPolicyTests
{
	[Fact]
	public void AFibrillatingHeart_WarrantsAShock() =>
		Assert.True(AedShockPolicy.IsShockAdvised(fibrillationProgress: 50f, fibrillationForced: false));

	[Fact]
	public void AForcedQuiver_WarrantsAShockEvenWithoutProgress() =>
		Assert.True(AedShockPolicy.IsShockAdvised(fibrillationProgress: 0f, fibrillationForced: true));

	[Fact]
	public void ACalmHeart_DoesNot() =>
		Assert.False(AedShockPolicy.IsShockAdvised(fibrillationProgress: 0f, fibrillationForced: false));
}
