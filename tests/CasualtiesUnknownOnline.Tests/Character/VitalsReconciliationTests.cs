using System;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using CasualtiesUnknownOnline.Runtime.Session.CharacterData;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Character;

/// <summary>
/// The minimal reconciliation: the host compares the report's three claimed values with the model
/// values it last wrote, BEFORE the report is filtered, and names every disagreement beyond the
/// recorded tolerances. The host's value still stands; the comparison is the only detector of a
/// wrong model (ruling item 5).
/// </summary>
public class VitalsReconciliationTests
{
	[Fact]
	public void AMatchingReportProducesNoDifference() =>
		Assert.Empty(VitalsReconciliation.Compare(Snapshot(80f, 0.5f, bleed: 5f), Snapshot(80f, 0.5f, bleed: 5f)));

	[Fact]
	public void ADifferenceInsideTheToleranceIsNotReported()
	{
		var differences = VitalsReconciliation.Compare(
			Snapshot(80f, 0.5f, bleed: 5f),
			Snapshot(80f + (VitalsReconciliation.BloodTolerance / 2f), 0.5f, bleed: 5f));

		Assert.Empty(differences);
	}

	[Fact]
	public void EveryOwnedValueIsNamedWithBothSides()
	{
		var differences = VitalsReconciliation.Compare(
			Snapshot(80f, 0.5f, bleed: 5f),
			Snapshot(70f, 0.2f, bleed: 2f));

		Assert.Contains(differences, line => line.Contains("blood volume", StringComparison.Ordinal)
			&& line.Contains("model 80.00", StringComparison.Ordinal)
			&& line.Contains("reported 70.00", StringComparison.Ordinal));
		Assert.Contains(differences, line => line.Contains("total bleeding speed", StringComparison.Ordinal));
		Assert.Contains(differences, line => line.Contains("limb 0 bleed", StringComparison.Ordinal));
	}

	[Fact]
	public void ALimbOnlyOneSideCarriesIsNotAValueDifference()
	{
		var model = Snapshot(80f, 0.5f, bleed: 5f);
		var reported = Snapshot(80f, 0.5f, bleed: 5f);
		reported.Limbs.Add(new CharacterLimbMsg { Index = 3, BleedAmount = 9f });

		Assert.Empty(VitalsReconciliation.Compare(model, reported));
	}

	[Fact]
	public void AMissingHealthBlockIsNotCompared() =>
		Assert.Empty(VitalsReconciliation.Compare(new CharacterDataMsg(), Snapshot(80f, 0.5f, bleed: 5f)));

	private static CharacterDataMsg Snapshot(float blood, float total, float bleed) => new()
	{
		Health = new CharacterHealthMsg { BloodVolume = blood, TotalBleedSpeed = total },
		Limbs = [new CharacterLimbMsg { Index = 0, BleedAmount = bleed, SkinHealth = 50f }],
	};
}
