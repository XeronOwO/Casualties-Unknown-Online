using CasualtiesUnknownOnline.Runtime.Session.PlayerInteraction;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Session;

/// <summary>
/// The CPR rhythm's physiology (the user's 2026-10-24 rulings: compressions act on blood pressure, ventilation on
/// oxygen, and the restart is one shared outcome). These cases pin the rule itself; the applier and the operator's
/// button are the same ticket's next stages.
/// </summary>
[Trait("Category", "Unit")]
public class CprRhythmPolicyTests
{
	[Fact]
	public void AGoodCadence_AddsTheFullPressureGain() =>
		Assert.Equal(CprRhythmPolicy.CompressionGain, CprRhythmPolicy.PressureGain(CprRhythmPolicy.IdealInterval, currentPressure: 10f, CprRhythmPolicy.CompressionCeiling));

	[Fact]
	public void PressingTooFast_EarnsHalf() =>
		Assert.Equal(CprRhythmPolicy.CompressionGain * 0.5f, CprRhythmPolicy.PressureGain(0.15f, currentPressure: 10f, CprRhythmPolicy.CompressionCeiling));

	[Fact]
	public void ALatePress_ScalesDownAndAPressPastTheSlowLine_AddsNothing()
	{
		var late = CprRhythmPolicy.PressureGain(1.1f, currentPressure: 10f, CprRhythmPolicy.CompressionCeiling);
		Assert.True(late > 0f && late < CprRhythmPolicy.CompressionGain);

		Assert.Equal(0f, CprRhythmPolicy.PressureGain(CprRhythmPolicy.TooSlowInterval, currentPressure: 10f, CprRhythmPolicy.CompressionCeiling));
	}

	[Fact]
	public void AtTheCeiling_MoreCompressionsAddNothing() =>
		Assert.Equal(0f, CprRhythmPolicy.PressureGain(CprRhythmPolicy.IdealInterval, CprRhythmPolicy.CompressionCeiling, CprRhythmPolicy.CompressionCeiling));

	[Fact]
	public void Ventilation_AddsOxygenUpToItsOwnCeiling()
	{
		Assert.Equal(CprRhythmPolicy.VentilationGain, CprRhythmPolicy.OxygenGain(CprRhythmPolicy.IdealInterval, currentOxygen: 20f, oxygenCeiling: 100f));
		Assert.Equal(0f, CprRhythmPolicy.OxygenGain(CprRhythmPolicy.IdealInterval, currentOxygen: 100f, oxygenCeiling: 100f));
	}

	[Fact]
	public void ALostBloodVolume_LowersTheCeilingWithIt() =>
		Assert.Equal(CprRhythmPolicy.CompressionCeiling * 0.5f, CprRhythmPolicy.CeilingFor(bloodVolumePercentage: 50f, hemothoraxProgress: 0f), 3);

	[Fact]
	public void AnOpenChest_HalvesTheCeiling() =>
		Assert.Equal(CprRhythmPolicy.FibrillationPressure * 0.5f, CprRhythmPolicy.CeilingFor(bloodVolumePercentage: 100f, hemothoraxProgress: 5f), 3);

	[Fact]
	public void TheHeartRestartsOnlyOncePerfusionReachesTheThreshold()
	{
		Assert.False(CprRhythmPolicy.RestartsTheHeart(pressureAfter: CprRhythmPolicy.RestartPressure - 1f, CprRhythmPolicy.CompressionCeiling));
		Assert.True(CprRhythmPolicy.RestartsTheHeart(pressureAfter: CprRhythmPolicy.RestartPressure, CprRhythmPolicy.CompressionCeiling));
	}

	[Fact]
	public void APatientWhoseCeilingIsBelowTheThreshold_RestartsAtItsOwnCeiling()
	{
		// Half the blood volume puts the ceiling at 44, below the 45 threshold: reaching the body's own limit is the best
		// this patient can do, and the rule says that counts.
		var ceiling = CprRhythmPolicy.CeilingFor(bloodVolumePercentage: 50f, hemothoraxProgress: 0f);
		Assert.True(CprRhythmPolicy.RestartsTheHeart(ceiling, ceiling));
	}
}
