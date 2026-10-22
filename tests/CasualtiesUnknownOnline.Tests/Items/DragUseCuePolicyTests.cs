using CasualtiesUnknownOnline.GameAdapter.Items;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Gate;

/// <summary>
/// When the drag gesture announces a target. The gesture is the only entry point for
/// cross-player use and it showed nothing at all, which the user read as a feature that
/// does not exist; the cue that fixes that must appear exactly when a release WOULD use
/// the item on someone, because a cue that appears otherwise is the misfire-as-a-bug the
/// same finding describes.
/// </summary>
public class DragUseCuePolicyTests
{
	private const ulong Target = 2002;

	[Fact]
	public void AnEligibleItemOverAPlayer_NamesThatPlayer()
	{
		var cue = DragUseCuePolicy.Decide(sessionActive: true, hasDraggedItem: true, itemIsEligible: true, Target);

		Assert.True(cue.Showing);
		Assert.Equal(Target, cue.TargetSteamId);
	}

	[Fact]
	public void AnItemTheFamilyGateRefuses_ShowsNothing()
	{
		// The cue teaches what can be handed over: showing it for an item the release would
		// drop instead would promise a target the gesture will not use.
		var cue = DragUseCuePolicy.Decide(true, true, itemIsEligible: false, Target);

		Assert.False(cue.Showing);
		Assert.Equal(0UL, cue.TargetSteamId);
	}

	[Fact]
	public void NoPlayerUnderTheCursor_ShowsNothing() =>
		Assert.False(DragUseCuePolicy.Decide(true, true, true, targetSteamId: null).Showing);

	// A row without an identity cannot be named, and naming it would be the label disagreeing
	// with the release.
	[Fact]
	public void AZeroTarget_ShowsNothing() =>
		Assert.False(DragUseCuePolicy.Decide(true, true, true, 0UL).Showing);

	[Fact]
	public void NothingDragged_ShowsNothing() =>
		Assert.False(DragUseCuePolicy.Decide(true, hasDraggedItem: false, true, Target).Showing);

	// Solo play is unchanged by this feature, and there is nobody to name.
	[Fact]
	public void WithoutASession_ShowsNothing() =>
		Assert.False(DragUseCuePolicy.Decide(sessionActive: false, true, true, Target).Showing);
}
