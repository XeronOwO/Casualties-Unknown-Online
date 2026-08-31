using System.Collections.Generic;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using CasualtiesUnknownOnline.Runtime.Session.Items;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Items;

/// <summary>
/// The pure evidence comparison (ItemArbitration.CheckEvidence): input =
/// authoritative entry + report evidence, output = the divergence verdict.
/// The side-effect execution (the sends the verdict demands) is covered by the
/// flow tests.
/// </summary>
public class ItemArbitrationVerdictTests
{
	private const ulong ItemId = 42;

	private static CharacterItemMsg Item(
		float condition = 1f,
		bool favourited = false,
		List<LiquidStackMsg>? liquids = null,
		List<ComponentStateMsg>? components = null,
		List<CharacterItemMsg>? contents = null) => new()
		{
			ItemId = "test_item",
			Condition = condition,
			Favourited = favourited,
			Liquids = liquids ?? [],
			Components = components ?? [],
			Contents = contents ?? [],
		};

	[Fact]
	public void ConsistentEvidence_Matches()
	{
		var verdict = ItemArbitration.CheckEvidence(ItemId, Item(), Item());

		Assert.True(verdict.Matches);
		Assert.False(verdict.NeedsCorrection);
		Assert.Empty(verdict.ExtraContentIds);
	}

	[Fact]
	public void NullEvidence_Matches_LegacyReportHasNothingToCheck()
	{
		var verdict = ItemArbitration.CheckEvidence(ItemId, Item(), null);

		Assert.True(verdict.Matches);
		Assert.Empty(verdict.ExtraContentIds);
	}

	[Fact]
	public void ConditionDivergence_NeedsCorrection()
	{
		var verdict = ItemArbitration.CheckEvidence(ItemId, Item(condition: 1f), Item(condition: 0.5f));

		Assert.False(verdict.Matches);
		Assert.True(verdict.NeedsCorrection);
	}

	[Fact]
	public void ConditionWithinTolerance_Matches()
	{
		var verdict = ItemArbitration.CheckEvidence(ItemId, Item(condition: 1f), Item(condition: 0.995f));

		Assert.True(verdict.Matches);
	}

	[Fact]
	public void FavouritedDivergence_NeedsCorrection()
	{
		var verdict = ItemArbitration.CheckEvidence(ItemId, Item(), Item(favourited: true));

		Assert.True(verdict.NeedsCorrection);
	}

	[Fact]
	public void LiquidDivergence_NeedsCorrection()
	{
		var authority = Item(liquids: [new LiquidStackMsg { LiquidId = "water", Amount = 50f }]);
		var evidence = Item(liquids: [new LiquidStackMsg { LiquidId = "water", Amount = 30f }]);

		var verdict = ItemArbitration.CheckEvidence(ItemId, authority, evidence);

		Assert.True(verdict.NeedsCorrection);
	}

	[Fact]
	public void ComponentDivergence_NeedsCorrection()
	{
		var authority = Item(components: [new ComponentStateMsg
		{
			TypeName = "Comp",
			Fields = [new ComponentFieldMsg { Name = "f", Kind = 1, FloatValue = 1f }],
		}]);
		var evidence = Item(components: [new ComponentStateMsg
		{
			TypeName = "Comp",
			Fields = [new ComponentFieldMsg { Name = "f", Kind = 1, FloatValue = 5f }],
		}]);

		var verdict = ItemArbitration.CheckEvidence(ItemId, authority, evidence);

		Assert.True(verdict.NeedsCorrection);
	}

	[Fact]
	public void ExtraContent_ListedForDestruction_StillMatches()
	{
		var verdict = ItemArbitration.CheckEvidence(ItemId, Item(), Item(contents: [new CharacterItemMsg { InstanceId = 999 }]));

		Assert.True(verdict.Matches, "claimed-but-unknown contents destroy, they never block the action");
		Assert.False(verdict.NeedsCorrection);
		Assert.Equal([999], verdict.ExtraContentIds);
	}

	[Fact]
	public void MissingContent_NeedsCorrection()
	{
		var verdict = ItemArbitration.CheckEvidence(ItemId, Item(contents: [new CharacterItemMsg { InstanceId = 7 }]), Item());

		Assert.False(verdict.Matches);
		Assert.True(verdict.NeedsCorrection);
	}

	[Fact]
	public void NestedEmptyClaims_NotMissing()
	{
		// The digest shape stops at ids: the evidence claims content 7 but not
		// its contents — that is "no claim", never "empty contents".
		var authority = Item(contents: [new CharacterItemMsg { InstanceId = 7, Contents = [new CharacterItemMsg { InstanceId = 8 }] }]);
		var evidence = Item(contents: [new CharacterItemMsg { InstanceId = 7 }]);

		var verdict = ItemArbitration.CheckEvidence(ItemId, authority, evidence);

		Assert.True(verdict.Matches);
	}

	[Fact]
	public void NestedExtraContent_ListedForDestruction()
	{
		var authority = Item(contents: [new CharacterItemMsg { InstanceId = 7 }]);
		var evidence = Item(contents: [new CharacterItemMsg { InstanceId = 7, Contents = [new CharacterItemMsg { InstanceId = 999 }] }]);

		var verdict = ItemArbitration.CheckEvidence(ItemId, authority, evidence);

		Assert.True(verdict.Matches);
		Assert.Equal([999], verdict.ExtraContentIds);
	}

	[Fact]
	public void AuthoritativeInstanceId_SetToTableKey()
	{
		var authority = Item();

		ItemArbitration.CheckEvidence(ItemId, authority, Item());

		Assert.Equal(ItemId, authority.InstanceId); // the correction's recipient locates its instance by it
	}
}
