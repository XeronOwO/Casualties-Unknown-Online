using CasualtiesUnknownOnline.Runtime.Session.PlayerInteraction;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Session;

/// <summary>
/// The CPR control's rhythm feedback (ticket `remote-medical-cpr.md`, stage 4's intelligence rule, read from the reference
/// mod's own handler): the verdict comes from the SAME band the physiology scores, and a low-intelligence operator loses
/// the affirmation and the detailed guide while keeping the miss feedback — the reference's `stupid` branch suppresses
/// exactly the "good timing" line and swaps the guide text.
/// </summary>
[Trait("Category", "Unit")]
public class CprRhythmFeedbackTests
{
	[Fact]
	public void APressInsideTheBand_IsGood() =>
		Assert.Equal(CprRhythmVerdict.Good, CprRhythmPolicy.JudgePress(CprRhythmPolicy.IdealInterval));

	[Fact]
	public void AShove_IsTooFast() =>
		Assert.Equal(CprRhythmVerdict.TooFast, CprRhythmPolicy.JudgePress(CprRhythmPolicy.FastestInterval * 0.5f));

	[Fact]
	public void APressUnderTheSlowEdge_IsTooSlow() =>
		Assert.Equal(CprRhythmVerdict.TooSlow, CprRhythmPolicy.JudgePress(CprRhythmPolicy.SlowestInterval + 0.05f));

	[Fact]
	public void AClearOperatorSeesTheAffirmation_AndALowIntelligenceOneDoesNot()
	{
		Assert.True(CprRhythmPolicy.ShowsTheVerdict(CprRhythmVerdict.Good, intelligence: 12f));
		Assert.False(CprRhythmPolicy.ShowsTheVerdict(CprRhythmVerdict.Good, intelligence: 11f));
	}

	[Fact]
	public void EveryoneSeesTheMisses()
	{
		Assert.True(CprRhythmPolicy.ShowsTheVerdict(CprRhythmVerdict.TooFast, intelligence: 1f));
		Assert.True(CprRhythmPolicy.ShowsTheVerdict(CprRhythmVerdict.TooSlow, intelligence: 1f));
		Assert.False(CprRhythmPolicy.ShowsTheVerdict(CprRhythmVerdict.None, intelligence: 20f));
	}
}
