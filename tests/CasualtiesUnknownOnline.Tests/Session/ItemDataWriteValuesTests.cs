using System.Collections.Generic;
using System.Linq;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using CasualtiesUnknownOnline.Runtime.Session.Commands;
using CasualtiesUnknownOnline.Runtime.Session.Items;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Session;

/// <summary>
/// The value half of a write: whether the asked-for value is legal against the
/// item's OWN captured state, and what it does to that capture. Pure — every
/// refusal and every before/after pair is pinned here instead of through a live
/// console, and the writes are parsed by the real grammar so a shape the parser
/// admits is the shape this class is asked about.
/// </summary>
public class ItemDataWriteValuesTests
{
	private const ulong Backpack = 4101;

	[Fact]
	public void Condition_ReplacesTheScalarAndReportsBothValues()
	{
		var item = Item();

		var applied = Apply(item, "slot 0 condition 0.25");

		Assert.True(applied.Ok, applied.Error);
		Assert.Equal("1", applied.Before);
		Assert.Equal("0.25", applied.After);
		Assert.Equal(0.25f, item.Condition);
	}

	/// <summary>A negative condition is not a state the game ever produces, so the write refuses it rather than storing poison.</summary>
	[Fact]
	public void Condition_BelowZero_IsRefused()
	{
		var item = Item();

		var applied = Apply(item, "slot 0 condition -5");

		Assert.False(applied.Ok);
		Assert.Contains("condition is 0 or more", applied.Error);
		Assert.Equal(1f, item.Condition);
	}

	[Fact]
	public void Favourited_ReplacesTheFlagInTheWordsTheReadHalfPrints()
	{
		var item = Item();

		var applied = Apply(item, "slot 0 favourited yes");

		Assert.True(applied.Ok, applied.Error);
		Assert.Equal("no", applied.Before);
		Assert.Equal("yes", applied.After);
		Assert.True(item.Favourited);
	}

	[Fact]
	public void Liquid_ReplacesTheNamedStacksAmountAndLeavesTheOtherStackAlone()
	{
		var item = Item();

		var applied = Apply(item, "slot 0 liquid water 500");

		Assert.True(applied.Ok, applied.Error);
		Assert.Equal("250 ml", applied.Before);
		Assert.Equal("500 ml", applied.After);
		Assert.Equal(500f, item.Liquids.Single(liquid => liquid.LiquidId == "water").Amount);
		Assert.Equal(10f, item.Liquids.Single(liquid => liquid.LiquidId == "oil").Amount);
	}

	[Fact]
	public void Liquid_ForAStackTheItemDoesNotCarry_IsRefusedNamingWhatItHolds()
	{
		var item = Item();

		var applied = Apply(item, "slot 0 liquid blood 100");

		Assert.False(applied.Ok);
		Assert.Contains("carries no 'blood'", applied.Error);
		Assert.Contains("water 250 ml", applied.Error);
	}

	[Fact]
	public void Liquid_BelowZero_IsRefused()
	{
		var item = Item();

		var applied = Apply(item, "slot 0 liquid water -1");

		Assert.False(applied.Ok);
		Assert.Contains("liquid amount is 0 or more", applied.Error);
	}

	[Theory]
	[InlineData("int", "mode", "7", 7, "2")]
	[InlineData("bool", "on", "yes", true, "false")]
	[InlineData("string", "label", "fresh", "fresh", "\"old\"")]
	[InlineData("enum", "state", "3", 3, "1")]
	public void Component_WritesTheFieldInItsOwnKind(string kind, string field, string text, object expected, string before)
	{
		var item = Item();

		var applied = Apply(item, $"slot 0 component BatteryItem {field} {text}");

		Assert.True(applied.Ok, $"{kind}: {applied.Error}");
		Assert.Equal(before, applied.Before);
		var written = item.Components.Single().Fields.Single(candidate => candidate.Name == field);
		Assert.Equal(expected, written.Kind switch
		{
			SaveableFieldKind.Int or SaveableFieldKind.Enum => written.IntValue,
			SaveableFieldKind.Bool => written.BoolValue,
			_ => written.StringValue,
		});
	}

	/// <summary>A string list is written the way the read half prints it — comma-separated — and an empty entry writes nothing.</summary>
	[Fact]
	public void Component_StringList_IsCommaSeparated()
	{
		var item = Item();

		Assert.True(Apply(item, "slot 0 component BatteryItem tags a,b,c").Ok);
		Assert.Equal(["a", "b", "c"], item.Components.Single().Fields.Single(field => field.Name == "tags").StringList);

		Assert.True(Apply(item, "slot 0 component BatteryItem tags a,,b").Ok);
		Assert.Equal(["a", "b"], item.Components.Single().Fields.Single(field => field.Name == "tags").StringList);
	}

	[Fact]
	public void Component_WithAValueThatIsNotTheFieldsKind_IsRefused()
	{
		var item = Item();

		var applied = Apply(item, "slot 0 component BatteryItem charge fast");

		Assert.False(applied.Ok);
		Assert.Contains("'fast' is not a float for BatteryItem.charge", applied.Error);
		Assert.Equal(0.75f, item.Components.Single().Fields.Single(field => field.Name == "charge").FloatValue);
	}

	[Fact]
	public void Component_TheItemDoesNotCarry_IsRefusedNamingWhatItHas()
	{
		var item = Item();

		var applied = Apply(item, "slot 0 component Radio set 1");

		Assert.False(applied.Ok);
		Assert.Contains("has no component 'Radio'", applied.Error);
		Assert.Contains("BatteryItem", applied.Error);
	}

	[Fact]
	public void ComponentField_TheComponentDoesNotCarry_IsRefusedNamingWhatItHas()
	{
		var item = Item();

		var applied = Apply(item, "slot 0 component BatteryItem voltage 1");

		Assert.False(applied.Ok);
		Assert.Contains("has no field 'voltage'", applied.Error);
		Assert.Contains("float charge", applied.Error);
	}

	[Fact]
	public void Remove_LiquidEmptiesThatStackAndLeavesTheOtherAlone()
	{
		var item = Item();

		var applied = ApplyRemove(item, "slot 0 liquid water");

		Assert.True(applied.Ok, applied.Error);
		Assert.Equal("250 ml", applied.Before);
		Assert.Equal("0 ml", applied.After);
		Assert.Equal(0f, item.Liquids.Single(liquid => liquid.LiquidId == "water").Amount);

		// Removing one liquid is not emptying the container: the other stack is untouched.
		Assert.Equal(10f, item.Liquids.Single(liquid => liquid.LiquidId == "oil").Amount);
	}

	[Fact]
	public void Remove_ComponentField_ResetsTheValueItsOwnKindDefaultsTo()
	{
		var item = Item();

		// Every kind the capture can hold, reset in one command: the field stays in the
		// capture (the game's own component still has it) and takes its kind's zero value.
		var applied = ApplyRemove(item, "slot 0 component BatteryItem charge component BatteryItem mode component BatteryItem on component BatteryItem label component BatteryItem tags component BatteryItem state");

		Assert.True(applied.Ok, applied.Error);
		var fields = item.Components.Single().Fields;
		Assert.Equal(0f, fields.Single(field => field.Name == "charge").FloatValue);
		Assert.Equal(0, fields.Single(field => field.Name == "mode").IntValue);
		Assert.False(fields.Single(field => field.Name == "on").BoolValue);
		Assert.Equal("", fields.Single(field => field.Name == "label").StringValue);
		Assert.Empty(fields.Single(field => field.Name == "tags").StringList);
		Assert.Equal(0, fields.Single(field => field.Name == "state").IntValue);
	}

	[Fact]
	public void Remove_OfAPropertyWithNoAbsentState_IsRefusedAndChangesNothing()
	{
		var item = Item();

		// The removal of a bounded property is refused by the PARSER, and this pins that specific message:
		// the value layer carries a twin of it that no input can reach, so an assertion matching only the
		// shared phrase would pass whichever of the two had produced it.
		var condition = ApplyRemove(item, "slot 0 condition");
		Assert.False(condition.Ok);
		Assert.Contains("no absent state to remove", condition.Error);
		Assert.Contains("Usage: /data remove", condition.Error);
		Assert.Equal(1f, item.Condition);

		var favourited = ApplyRemove(item, "slot 0 favourited");
		Assert.False(favourited.Ok);
		Assert.Contains("Usage: /data remove", favourited.Error);
		Assert.False(item.Favourited);
	}

	[Fact]
	public void Remove_OfALiquidTheItemDoesNotCarry_IsRefusedNamingWhatItHas()
	{
		var item = Item();

		var applied = ApplyRemove(item, "slot 0 liquid petrol");

		Assert.False(applied.Ok);
		Assert.Contains("carries no 'petrol'", applied.Error);
		Assert.Contains("water", applied.Error);
		Assert.Equal(250f, item.Liquids.Single(liquid => liquid.LiquidId == "water").Amount);
	}

	/// <summary>Parses the real grammar and applies it — the two halves are pinned together, not separately.</summary>
	private static (bool Ok, string Before, string After, string? Error) Apply(CharacterItemMsg item, string line) =>
		ItemDataWriteParser.TryParse(line.Split(' '), out var write, out var parseError)
			? ItemDataWriteValues.TryApply(item, write, out var before, out var after, out var error)
				? (true, before, after, error)
				: (false, before, after, error)
			: (false, "", "", parseError);

	/// <summary>The same, through the remove grammar: the properties that verb accepts carry no value of their own.</summary>
	private static (bool Ok, string Before, string After, string? Error) ApplyRemove(CharacterItemMsg item, string line) =>
		ItemDataWriteParser.TryParseRemove(line.Split(' '), out var writes, out var parseError)
			? ApplyAll(item, writes)
			: (false, "", "", parseError);

	private static (bool Ok, string Before, string After, string? Error) ApplyAll(CharacterItemMsg item, IReadOnlyList<ItemDataWrite> writes)
	{
		var before = "";
		var after = "";
		foreach (var write in writes)
		{
			if (!ItemDataWriteValues.TryApply(item, write, out before, out after, out var error))
			{
				return (false, before, after, error);
			}
		}

		return (true, before, after, null);
	}

	private static CharacterItemMsg Item() => new()
	{
		InstanceId = Backpack,
		ItemId = "backpack",
		Condition = 1f,
		Liquids =
		[
			new LiquidStackMsg { LiquidId = "water", Amount = 250f },
			new LiquidStackMsg { LiquidId = "oil", Amount = 10f },
		],
		Components =
		[
			new ComponentStateMsg
			{
				TypeName = "BatteryItem",
				Fields =
				[
					new ComponentFieldMsg { Name = "charge", Kind = SaveableFieldKind.Float, FloatValue = 0.75f },
					new ComponentFieldMsg { Name = "mode", Kind = SaveableFieldKind.Int, IntValue = 2 },
					new ComponentFieldMsg { Name = "on", Kind = SaveableFieldKind.Bool },
					new ComponentFieldMsg { Name = "label", Kind = SaveableFieldKind.String, StringValue = "old" },
					new ComponentFieldMsg { Name = "tags", Kind = SaveableFieldKind.StringList, StringList = ["a"] },
					new ComponentFieldMsg { Name = "state", Kind = SaveableFieldKind.Enum, IntValue = 1 },
				],
			},
		],
	};
}
