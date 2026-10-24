using CasualtiesUnknownOnline.Runtime.Session.PlayerInteraction;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Session;

/// <summary>
/// The operator's own refusals (ticket `remote-medical-cpr.md`, stage 4): the reference's strength gate
/// (`skills.STR * handMaxTotalForce &gt; 6`) and the stamina floor, both answered on the operator's client, plus the two
/// catalogue keys the player's line comes from.
/// </summary>
[Trait("Category", "Unit")]
public class CprOperatorGateTests
{
	[Fact]
	public void AStrongOperator_PassesTheReferenceGate() =>
		Assert.True(CprOperatorGate.CanCompress(strength: 5, handMaxTotalForce: 2));

	[Fact]
	public void TheThresholdMustBeEXCEEDED() =>
		Assert.False(CprOperatorGate.CanCompress(strength: 3, handMaxTotalForce: 2));

	[Fact]
	public void AHandlessBody_CanNeverCompress() =>
		Assert.False(CprOperatorGate.CanCompress(strength: 20, handMaxTotalForce: 0f));

	[Fact]
	public void TheTwoRefusals_AreNamedSeparately()
	{
		// One is training, the other is rest: the player's fix differs, so the client's two lines do.
		Assert.Equal(CprOperatorRefusal.TooWeak, CprButtonPolicy.OperatorRefusal(canCompress: false, hasStamina: true));
		Assert.Equal(CprOperatorRefusal.Exhausted, CprButtonPolicy.OperatorRefusal(canCompress: true, hasStamina: false));
		Assert.Equal(CprOperatorRefusal.None, CprButtonPolicy.OperatorRefusal(canCompress: true, hasStamina: true));
	}
}
