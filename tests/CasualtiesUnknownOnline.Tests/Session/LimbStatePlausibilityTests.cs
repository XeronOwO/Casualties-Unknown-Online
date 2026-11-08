using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using CasualtiesUnknownOnline.Runtime.Session.CharacterData;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Session;

/// <summary>
/// The host's plausibility bound on a member's full limb report: a transition the game's own rates cannot produce
/// between the host's previous record and the report is refused whole. The console's whole-body heal is the case
/// that measured the bound.
/// </summary>
public sealed class LimbStatePlausibilityTests
{
	[Fact]
	public void TheFirstReportPasses()
	{
		var reason = LimbStatePlausibility.Check([], [Limb(0)]);

		Assert.Equal(LimbStateRefusalReason.None, reason);
	}

	[Fact]
	public void OrdinaryDeltasPass()
	{
		var previous = new[] { Limb(0, skin: 80f, muscle: 90f, infection: 20f) };
		var reported = new[] { Limb(0, skin: 81f, muscle: 90f, infection: 19f) };

		var reason = LimbStatePlausibility.Check(previous, reported);

		Assert.Equal(LimbStateRefusalReason.None, reason);
	}

	[Fact]
	public void AReportThatReturnsADismemberedLimbIsRefused()
	{
		var previous = new[] { Limb(0, dismembered: true) };
		var reported = new[] { Limb(0, dismembered: false) };

		var reason = LimbStatePlausibility.Check(previous, reported);

		Assert.Equal(LimbStateRefusalReason.DismemberedLimbReturned, reason);
	}

	[Fact]
	public void ASkinJumpBeyondTheGamesHealRateIsRefused()
	{
		var previous = new[] { Limb(0, skin: 10f) };
		var reported = new[] { Limb(0, skin: 100f) };

		var reason = LimbStatePlausibility.Check(previous, reported);

		Assert.Equal(LimbStateRefusalReason.SkinJumped, reason);
	}

	[Fact]
	public void AMuscleJumpBeyondTheGamesHealRateIsRefused()
	{
		var previous = new[] { Limb(0, muscle: 10f) };
		var reported = new[] { Limb(0, muscle: 100f) };

		var reason = LimbStatePlausibility.Check(previous, reported);

		Assert.Equal(LimbStateRefusalReason.MuscleJumped, reason);
	}

	[Fact]
	public void InfectionClearingFasterThanTheCureIsRefused()
	{
		var previous = new[] { Limb(0, infection: 100f) };
		var reported = new[] { Limb(0, infection: 0f) };

		var reason = LimbStatePlausibility.Check(previous, reported);

		Assert.Equal(LimbStateRefusalReason.InfectionCleared, reason);
	}

	private static CharacterLimbMsg Limb(int index, float skin = 100f, float muscle = 100f, float infection = 0f, bool dismembered = false) =>
		new()
		{
			Index = index,
			SkinHealth = skin,
			MuscleHealth = muscle,
			InfectionAmount = infection,
			Dismembered = dismembered,
		};
}
