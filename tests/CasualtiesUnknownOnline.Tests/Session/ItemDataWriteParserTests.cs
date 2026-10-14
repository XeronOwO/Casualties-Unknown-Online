using CasualtiesUnknownOnline.Runtime.Session.Commands;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Session;

/// <summary>
/// The item-data write grammar: which shapes name a property to write, with what
/// value, and which are refused. Pure on purpose (the command handler owns the
/// session and the scene) — a shape a player can type is a decision this class
/// records rather than one the handler happens to make.
///
/// <para>
/// The TARGET half is the read verb's own grammar, so its shapes are pinned by
/// <see cref="ItemDataQueryParserTests"/>; what this class pins is that a write
/// admits them unchanged, that the listing shape is admitted here and refused at
/// execution, and that the property vocabulary is closed.
/// </para>
/// </summary>
public class ItemDataWriteParserTests
{
	[Fact]
	public void Condition_AddressesTheScalarOnTheNamedSlot()
	{
		Assert.True(ItemDataWriteParser.TryParse(["slot", "0", "condition", "0.5"], out var write, out var error));

		Assert.Null(error);
		Assert.Equal(ItemDataQueryKind.Slot, write.Target.Kind);
		Assert.Null(write.Target.Selector);
		Assert.Equal(0, write.Target.Slot);
		Assert.Equal(ItemDataProperty.Condition, write.Property);
		Assert.Equal(0.5f, write.Number);
	}

	[Fact]
	public void InstanceIdWithAFavouritedFlag_AddressesOneItem()
	{
		Assert.True(ItemDataWriteParser.TryParse(["id", "4101", "favourited", "true"], out var write, out _));

		Assert.Equal(ItemDataQueryKind.InstanceId, write.Target.Kind);
		Assert.Equal(4101UL, write.Target.InstanceId);
		Assert.Equal(ItemDataProperty.Favourited, write.Property);
		Assert.True(write.Flag);
	}

	[Fact]
	public void Liquid_NamesTheStackAndItsMillilitres()
	{
		Assert.True(ItemDataWriteParser.TryParse(["@a", "slot", "2", "liquid", "water", "250"], out var write, out _));

		Assert.Equal(ItemDataQueryKind.Slot, write.Target.Kind);
		Assert.Equal("@a", write.Target.Selector);
		Assert.Equal(2, write.Target.Slot);
		Assert.Equal(ItemDataProperty.Liquid, write.Property);
		Assert.Equal("water", write.Name);
		Assert.Equal(250f, write.Number);
	}

	[Fact]
	public void Component_NamesTheTypeTheFieldAndTheValueAsWritten()
	{
		Assert.True(ItemDataWriteParser.TryParse(["id", "7", "component", "BatteryItem", "charge", "0.75"], out var write, out _));

		Assert.Equal(ItemDataProperty.Component, write.Property);
		Assert.Equal("BatteryItem", write.Component);
		Assert.Equal("charge", write.Name);
		Assert.Equal("0.75", write.Text);
	}

	/// <summary>
	/// The target grammar stays as wide as the read verb's on purpose: a shape that
	/// names several items is parsed here and refused where the resolved set is
	/// visible (user ruling 2026-10-14), so the verdict is about the session rather
	/// than about the words.
	/// </summary>
	[Fact]
	public void AShapeThatNamesNoSingleItem_IsStillParsed()
	{
		Assert.True(ItemDataWriteParser.TryParse(["@s", "condition", "1"], out var write, out _));

		Assert.Equal(ItemDataQueryKind.Bodies, write.Target.Kind);
		Assert.Equal("@s", write.Target.Selector);
	}

	[Theory]
	[InlineData("true", true)]
	[InlineData("TRUE", true)]
	[InlineData("yes", true)]
	[InlineData("on", true)]
	[InlineData("false", false)]
	[InlineData("No", false)]
	[InlineData("off", false)]
	public void Flag_AcceptsEveryWordTheVerbPrintsOrOffers(string text, bool expected)
	{
		Assert.True(ItemDataWriteParser.TryParse(["slot", "0", "favourited", text], out var write, out _));

		Assert.Equal(expected, write.Flag);
	}

	[Theory]
	[InlineData("CONDITION")]
	[InlineData("Condition")]
	public void PropertyKeywords_AreCaseInsensitive(string keyword)
	{
		Assert.True(ItemDataWriteParser.TryParse(["slot", "0", keyword, "1"], out var write, out _));

		Assert.Equal(ItemDataProperty.Condition, write.Property);
	}

	/// <summary>
	/// The parser admits what the DATA MODEL can hold; whether the value is legal for
	/// the item is decided at execution, against the item's own state — a negative
	/// condition parses here and is refused by the value check.
	/// </summary>
	[Fact]
	public void AValueTheItemWillRefuse_IsStillParsed()
	{
		Assert.True(ItemDataWriteParser.TryParse(["slot", "0", "condition", "-5"], out var write, out var error));

		Assert.Null(error);
		Assert.Equal(-5f, write.Number);
	}

	[Theory]
	[InlineData("")]
	[InlineData("slot 0")]
	[InlineData("slot 0 nonsense 1")]
	[InlineData("slot 0 condition")]
	[InlineData("slot 0 condition abc")]
	[InlineData("slot 0 condition 1 2")]
	[InlineData("slot 0 condition NaN")]
	[InlineData("slot 0 condition Infinity")]
	[InlineData("slot 0 favourited maybe")]
	[InlineData("slot 0 favourited")]
	[InlineData("slot 0 liquid water")]
	[InlineData("slot 0 liquid water abc")]
	[InlineData("slot 0 liquid water 1 2")]
	[InlineData("slot 0 component BatteryItem charge")]
	[InlineData("slot 0 component BatteryItem charge 0.5 extra")]
	[InlineData("4101 condition 1")]
	[InlineData("id")]
	public void AnythingElse_IsRefusedWithTheUsage(string text)
	{
		Assert.False(ItemDataWriteParser.TryParse(text.Length == 0 ? [] : text.Split(' '), out _, out var error));

		Assert.NotNull(error);
		Assert.Contains("Usage:", error);
	}

	[Fact]
	public void AnUnknownProperty_IsNamedInTheRefusal()
	{
		Assert.False(ItemDataWriteParser.TryParse(["slot", "0", "durablity", "1"], out _, out var error));

		Assert.NotNull(error);
		Assert.Contains("'durablity' is not a property to write", error);
		Assert.Contains("condition", error);
	}
}
