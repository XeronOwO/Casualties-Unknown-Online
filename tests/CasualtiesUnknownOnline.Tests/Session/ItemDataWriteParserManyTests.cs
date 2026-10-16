using CasualtiesUnknownOnline.Runtime.Session.Commands;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Session;

/// <summary>
/// The two write verbs that name MORE than one property: <c>merge</c> takes one
/// target and several property/value pairs, <c>remove</c> one target and several
/// properties with no value. Split from the parser's own class so the two families
/// are not serialized in one xUnit collection, and because what these pin is the
/// difference between the verbs rather than the property vocabulary itself.
/// </summary>
public class ItemDataWriteParserManyTests
{
	[Fact]
	public void Merge_TakesOneTargetAndSeveralProperties()
	{
		Assert.True(ItemDataWriteParser.TryParseMany(["slot", "1", "condition", "0.75", "favourited", "yes", "liquid", "water", "300"], out var writes, out var error));

		Assert.Null(error);
		Assert.Equal(3, writes.Count);
		Assert.All(writes, write => Assert.Equal(ItemDataQueryKind.Slot, write.Target.Kind));
		Assert.Equal(ItemDataProperty.Condition, writes[0].Property);
		Assert.Equal(0.75f, writes[0].Number);
		Assert.Equal(ItemDataProperty.Favourited, writes[1].Property);
		Assert.True(writes[1].Flag);
		Assert.Equal(ItemDataProperty.Liquid, writes[2].Property);
		Assert.Equal("water", writes[2].Name);
		Assert.Equal(300f, writes[2].Number);

		// Every property in a merge edits the ONE item the target named: naming it per
		// property is what would let one command edit two items.
		Assert.All(writes, write => Assert.Equal(1, write.Target.Slot));
		Assert.All(writes, write => Assert.False(write.Clear));
	}

	[Fact]
	public void Merge_WithATargetAndNoProperty_IsRefused()
	{
		Assert.False(ItemDataWriteParser.TryParseMany(["slot", "1"], out _, out var error));
		Assert.Contains("Usage: /data merge", error);
	}

	[Fact]
	public void Merge_StopsAtAPropertyThatIsNotOne()
	{
		Assert.False(ItemDataWriteParser.TryParseMany(["slot", "1", "condition", "0.5", "colour", "red"], out _, out var error));
		Assert.Contains("'colour' is not a property to write", error);
	}

	[Fact]
	public void Merge_RefusesAPropertyMissingItsValue()
	{
		Assert.False(ItemDataWriteParser.TryParseMany(["slot", "1", "condition"], out _, out var error));
		Assert.Contains("Usage: /data set", error);
	}

	[Fact]
	public void Remove_NamesPropertiesWithNoValue()
	{
		Assert.True(ItemDataWriteParser.TryParseRemove(["id", "77", "liquid", "water", "component", "Battery", "charge"], out var writes, out var error));

		Assert.Null(error);
		Assert.Equal(2, writes.Count);
		Assert.All(writes, write => Assert.True(write.Clear));
		Assert.Equal(ItemDataProperty.Liquid, writes[0].Property);
		Assert.Equal("water", writes[0].Name);
		Assert.Equal(ItemDataProperty.Component, writes[1].Property);
		Assert.Equal("Battery", writes[1].Component);
		Assert.Equal("charge", writes[1].Name);
		Assert.Equal(77UL, writes[1].Target.InstanceId);
	}

	[Fact]
	public void Remove_OfAPropertyWithNoAbsentState_IsRefusedByName()
	{
		// A condition and a favourite flag are values with a range; turning "remove" into
		// "write the range's floor" would mean something different for a battery than for
		// a condition, so the verb refuses instead of choosing for the operator.
		Assert.False(ItemDataWriteParser.TryParseRemove(["slot", "1", "condition"], out _, out var error));
		Assert.Contains("no absent state to remove", error);
		Assert.Contains("accepts liquid and component", error);

		Assert.False(ItemDataWriteParser.TryParseRemove(["slot", "1", "favourited"], out _, out var error2));
		Assert.Contains("no absent state to remove", error2);
	}

	[Fact]
	public void Remove_RefusesAPropertyMissingItsOperand()
	{
		Assert.False(ItemDataWriteParser.TryParseRemove(["slot", "1", "liquid"], out _, out var error));
		Assert.Contains("needs the liquid's id", error);

		Assert.False(ItemDataWriteParser.TryParseRemove(["slot", "1", "component", "Battery"], out _, out var error2));
		Assert.Contains("needs the component's type name and the field's name", error2);
	}

	[Fact]
	public void Remove_WithNoPropertyAtAll_IsRefused()
	{
		Assert.False(ItemDataWriteParser.TryParseRemove(["slot", "1"], out _, out var error));
		Assert.Contains("Usage: /data remove", error);
	}
}
