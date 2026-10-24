using CasualtiesUnknownOnline.Runtime.Session.PlayerInteraction;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Session;

/// <summary>
/// What a cross-player item use needs from the TARGET (the user's 2026-10-24 finding: an unconscious teammate is
/// exactly who the medical families exist for). The split follows the medical operation family's own rule: the
/// families that APPLY something to a body work on an unresponsive one, and only the two where the target itself
/// consumes the item need it conscious.
/// </summary>
[Trait("Category", "Unit")]
public class ItemUseTargetPolicyTests
{
	[Theory]
	[InlineData(ItemUseFamily.Injection)]
	[InlineData(ItemUseFamily.Topical)]
	[InlineData(ItemUseFamily.Wear)]
	[InlineData(ItemUseFamily.LimbTool)]
	public void TheApplyingFamilies_WorkOnAnUnconsciousTarget(ItemUseFamily family) =>
		Assert.False(ItemUseTargetPolicy.RequiresAConsciousTarget(family));

	[Theory]
	[InlineData(ItemUseFamily.Drink)]
	[InlineData(ItemUseFamily.SolidFood)]
	public void TheConsumingFamilies_NeedAConsciousTarget(ItemUseFamily family) =>
		Assert.True(ItemUseTargetPolicy.RequiresAConsciousTarget(family));
}
