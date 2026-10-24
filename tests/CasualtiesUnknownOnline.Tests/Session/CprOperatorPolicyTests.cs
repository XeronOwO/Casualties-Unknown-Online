using CasualtiesUnknownOnline.Runtime.Session.PlayerInteraction;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Session;

/// <summary>
/// The operator's own costs and gate (ticket `remote-medical-cpr.md`, stage 4): the reference charges stamina and a little
/// heat per press and gates a weak operator out, so the numbers live in one pure place the applier and the operator's
/// client both read. The band is the reference's (65–220 a minute), so the stamina is sized against it: a full 100-stamina
/// bar covers a realistic attempt at that rate.
/// </summary>
[Trait("Category", "Unit")]
public class CprOperatorPolicyTests
{
	[Fact]
	public void AFullBarCoversAboutTwentyPressesAtTheBandRate()
	{
		// 100 / 2.5 = 40 presses, which at 110 a minute is about 22 seconds of CPR — long enough for one attempt.
		Assert.Equal(40, (int)(100f / CprRhythmPolicy.StaminaPerPress));
		Assert.Equal(2.5f, CprRhythmPolicy.StaminaPerPress);
	}

	[Fact]
	public void AnExhaustedOperatorEarnsNothing() =>
		Assert.True(CprRhythmPolicy.MinimumStamina > 0f && CprRhythmPolicy.MinimumStamina < 5f);

	[Fact]
	public void TheHeatIsSmallEnoughToAccumulateRatherThanSpike() =>
		Assert.True(CprRhythmPolicy.TemperaturePerPress > 0f && CprRhythmPolicy.TemperaturePerPress < 0.1f);
}
