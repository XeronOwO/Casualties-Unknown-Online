using CasualtiesUnknownOnline.Runtime.Session.Commands;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Session;

/// <summary>
/// The item-data target grammar: what each token shape resolves to, and what is
/// refused. The parser is pure on purpose — the vocabulary is pinned here instead
/// of through a live console, so a target a player can type is a decision this
/// class records rather than one the handler happens to make.
/// </summary>
public class ItemDataQueryParserTests
{
	[Fact]
	public void NoTarget_AddressesTheCallersOwnBody()
	{
		Assert.True(ItemDataQueryParser.TryParse([], out var query, out var error));

		Assert.Null(error);
		Assert.Equal(ItemDataQueryKind.Bodies, query.Kind);
		Assert.Null(query.Selector);
	}

	[Theory]
	[InlineData("@s")]
	[InlineData("@a")]
	[InlineData("@a[limit=2,sort=nearest]")]
	public void Selector_AddressesTheBodiesItNames(string selector)
	{
		Assert.True(ItemDataQueryParser.TryParse([selector], out var query, out var error));

		Assert.Null(error);
		Assert.Equal(ItemDataQueryKind.Bodies, query.Kind);
		Assert.Equal(selector, query.Selector);
	}

	[Fact]
	public void Slot_AddressesTheCallersOwnSlot()
	{
		Assert.True(ItemDataQueryParser.TryParse(["slot", "3"], out var query, out var error));

		Assert.Null(error);
		Assert.Equal(ItemDataQueryKind.Slot, query.Kind);
		Assert.Null(query.Selector);
		Assert.Equal(3, query.Slot);
	}

	[Fact]
	public void SelectorWithSlot_AddressesThatBodysSlot()
	{
		Assert.True(ItemDataQueryParser.TryParse(["@a", "slot", "0"], out var query, out var error));

		Assert.Null(error);
		Assert.Equal(ItemDataQueryKind.Slot, query.Kind);
		Assert.Equal("@a", query.Selector);
		Assert.Equal(0, query.Slot);
	}

	[Fact]
	public void InstanceId_AddressesOneItemWhereverItIs()
	{
		Assert.True(ItemDataQueryParser.TryParse(["id", "4101"], out var query, out var error));

		Assert.Null(error);
		Assert.Equal(ItemDataQueryKind.InstanceId, query.Kind);
		Assert.Equal(4101UL, query.InstanceId);
	}

	[Theory]
	[InlineData("ID")]
	[InlineData("Id")]
	public void Keywords_AreCaseInsensitive(string keyword)
	{
		Assert.True(ItemDataQueryParser.TryParse([keyword, "7"], out var query, out var error));

		Assert.Null(error);
		Assert.Equal(ItemDataQueryKind.InstanceId, query.Kind);
		Assert.Equal(7UL, query.InstanceId);
	}

	[Fact]
	public void SlotKeyword_IsCaseInsensitive()
	{
		Assert.True(ItemDataQueryParser.TryParse(["Slot", "2"], out var query, out _));

		Assert.Equal(ItemDataQueryKind.Slot, query.Kind);
		Assert.Equal(2, query.Slot);
	}

	/// <summary>
	/// Every shape that is not a target is refused WITH the usage line. The bare
	/// number is the important negative: reading it as an instance id would let a
	/// typo address an item the player never named.
	/// </summary>
	[Theory]
	[InlineData("id")]
	[InlineData("id abc")]
	[InlineData("id 1 2")]
	[InlineData("id -1")]
	[InlineData("slot")]
	[InlineData("slot -1")]
	[InlineData("slot x")]
	[InlineData("slot 1 2")]
	[InlineData("4101")]
	[InlineData("bandage")]
	[InlineData("@a slot")]
	[InlineData("@a slot x")]
	[InlineData("@a extra")]
	[InlineData("@a slot 1 2")]
	[InlineData("@")]
	public void InvalidTarget_IsRefusedWithTheUsage(string text)
	{
		Assert.False(ItemDataQueryParser.TryParse(text.Split(' '), out _, out var error));

		Assert.NotNull(error);
		Assert.Contains("Usage:", error);
	}
}
