using CasualtiesUnknownOnline.Runtime.Session.CharacterData;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Character;

/// <summary>
/// The injury trigger: a rise in any limb's bleed is the owner-judged injury that the plain report
/// may not carry, while the host's own decay and the 10 Hz row only lower the value. The epsilon
/// keeps float noise out and the smallest real wound well above it.
/// </summary>
public class PlayerInjuryRuleTests
{
	[Fact]
	public void ARiseBeyondTheEpsilonIsAnInjury() =>
		Assert.True(PlayerInjuryRule.AnyBleedRose([1f, 2f], [1f, 2.5f]));

	[Fact]
	public void ADecayIsNotAnInjury() =>
		Assert.False(PlayerInjuryRule.AnyBleedRose([5f, 0f], [4.9f, 0f]));

	[Fact]
	public void FloatNoiseIsNotAnInjury() =>
		Assert.False(PlayerInjuryRule.AnyBleedRose([5f], [5f + (PlayerInjuryRule.BleedIncreaseEpsilon / 2f)]));

	[Fact]
	public void AMismatchedLengthIsComparedOnTheCommonPart()
	{
		Assert.False(PlayerInjuryRule.AnyBleedRose([5f], [5f, 100f]));
		Assert.True(PlayerInjuryRule.AnyBleedRose([5f], [6f, 100f]));
	}
}
