using System.Collections.Generic;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using CasualtiesUnknownOnline.Runtime.Session.Items;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Items;

/// <summary>
/// The pure top-level item-state comparison shared by the arbitration
/// evidence check and the periodic keyframe reconcile. The tolerance rules
/// live here once; the GameAdapter's ItemReconcile asks the same verdict
/// before writing component/liquid state.
/// </summary>
public class ItemStateEqualityTests
{
	private static CharacterItemMsg Item(
		float condition = 1f,
		bool favourited = false,
		List<LiquidStackMsg>? liquids = null,
		List<ComponentStateMsg>? components = null) => new()
		{
			Condition = condition,
			Favourited = favourited,
			Liquids = liquids ?? [],
			Components = components ?? [],
		};

	[Fact]
	public void EqualTopLevel_Matches()
	{
		var a = Item(condition: 0.5f, favourited: true);
		var b = Item(condition: 0.5f, favourited: true);

		Assert.True(ItemStateEquality.TopLevelMatches(a, b));
	}

	[Fact]
	public void ConditionOutsideTolerance_DoesNotMatch()
	{
		var a = Item(condition: 0.5f);
		var b = Item(condition: 0.52f);

		Assert.False(ItemStateEquality.TopLevelMatches(a, b));
		Assert.True(ItemStateEquality.TopLevelMatches(a, b, conditionTolerance: 0.05f));
	}

	[Fact]
	public void FavouritedMismatch_DoesNotMatch() =>
		Assert.False(ItemStateEquality.TopLevelMatches(Item(favourited: false), Item(favourited: true)));

	[Fact]
	public void LiquidsMismatch_DoesNotMatch()
	{
		var a = Item(liquids: [new LiquidStackMsg { LiquidId = "water", Amount = 0.5f }]);
		var b = Item(liquids: [new LiquidStackMsg { LiquidId = "water", Amount = 0.7f }]);

		Assert.False(ItemStateEquality.TopLevelMatches(a, b));
	}

	[Fact]
	public void ComponentsMismatch_DoesNotMatch()
	{
		var a = Item(components:
		[
			new ComponentStateMsg
			{
				TypeName = "CustomItemBehaviour",
				Fields = [new ComponentFieldMsg { Name = "state", Kind = 2, IntValue = 1 }],
			},
		]);
		var b = Item(components:
		[
			new ComponentStateMsg
			{
				TypeName = "CustomItemBehaviour",
				Fields = [new ComponentFieldMsg { Name = "state", Kind = 2, IntValue = 2 }],
			},
		]);

		Assert.False(ItemStateEquality.TopLevelMatches(a, b));
	}

	[Fact]
	public void ComponentsEqual_MatchingOrderInsensitive()
	{
		var a = Item(components:
		[
			new ComponentStateMsg
			{
				TypeName = "CustomItemBehaviour",
				Fields = [new ComponentFieldMsg { Name = "state", Kind = 2, IntValue = 1 }],
			},
		]);
		var b = Item(components:
		[
			new ComponentStateMsg
			{
				TypeName = "CustomItemBehaviour",
				Fields = [new ComponentFieldMsg { Name = "state", Kind = 2, IntValue = 1 }],
			},
		]);

		Assert.True(ItemStateEquality.TopLevelMatches(a, b));
	}

	[Fact]
	public void FieldEquals_HandlesEverySupportedKind()
	{
		Assert.True(ItemStateEquality.FieldEquals(
			new ComponentFieldMsg { Name = "f", Kind = 1, FloatValue = 0.5f },
			new ComponentFieldMsg { Name = "f", Kind = 1, FloatValue = 0.51f }));
		Assert.True(ItemStateEquality.FieldEquals(
			new ComponentFieldMsg { Name = "i", Kind = 2, IntValue = 7 },
			new ComponentFieldMsg { Name = "i", Kind = 2, IntValue = 7 }));
		Assert.True(ItemStateEquality.FieldEquals(
			new ComponentFieldMsg { Name = "b", Kind = 3, BoolValue = true },
			new ComponentFieldMsg { Name = "b", Kind = 3, BoolValue = true }));
		Assert.True(ItemStateEquality.FieldEquals(
			new ComponentFieldMsg { Name = "s", Kind = 4, StringValue = "x" },
			new ComponentFieldMsg { Name = "s", Kind = 4, StringValue = "x" }));
		Assert.True(ItemStateEquality.FieldEquals(
			new ComponentFieldMsg { Name = "l", Kind = 5, StringList = ["a", "b"] },
			new ComponentFieldMsg { Name = "l", Kind = 5, StringList = ["a", "b"] }));
		Assert.True(ItemStateEquality.FieldEquals(
			new ComponentFieldMsg { Name = "e", Kind = 6, IntValue = 3 },
			new ComponentFieldMsg { Name = "e", Kind = 6, IntValue = 3 }));
		Assert.False(ItemStateEquality.FieldEquals(
			new ComponentFieldMsg { Name = "f", Kind = 1, FloatValue = 0.5f },
			new ComponentFieldMsg { Name = "f", Kind = 1, FloatValue = 0.6f }));
	}
}
