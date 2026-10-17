using System.Collections.Generic;
using CasualtiesUnknownOnline.GameState;
using CasualtiesUnknownOnline.GameState.Domains.Items;
using CasualtiesUnknownOnline.Runtime.Data;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using CasualtiesUnknownOnline.Runtime.Session.Commands;
using CasualtiesUnknownOnline.Tests.Fakes;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Session;

/// <summary>
/// What the item-data verb prints: the item's own state as the ONE data language, around it
/// the location, the revision and the direct contents, and a write's answer with the clauses
/// built from the state before and the state the adapter re-read. Pure — the kernel and the
/// session are not involved — because the wording is the contract the operator reads and a
/// later write has to name.
/// </summary>
public class ItemDataRendererTests
{
	private const ulong Owner = 1001;

	[Fact]
	public void One_RendersTheStateDocument()
	{
		var item = Item(4101, ItemLocation.Carried(new ActorId(Owner)), new ItemData(
			0.8f,
			true,
			1,
			[new ItemLiquidStack("water", 250f)],
			[Component("BatteryItem", Field("charge", ItemComponentFieldKind.Float, floatValue: 0.75f))]));

		var text = ItemDataRenderer.One(item, FakeItemState.Describe(item.Data), [item]);

		Assert.Contains("#4101 backpack [carried by 1001, slot 1] revision 7", text);
		Assert.Contains(SnbtWriter.Write(FakeItemState.Describe(item.Data)), text);
		Assert.Contains("Condition:0.8f", text);
		Assert.Contains("LiquidId:", text);
		Assert.Contains("Components:{BatteryItem:{charge:0.75f}}", text);
	}

	[Fact]
	public void One_RendersEachFieldKindInTheCanonicalForm()
	{
		var item = Item(4101, ItemLocation.Carried(new ActorId(Owner)), new ItemData(
			1f,
			false,
			0,
			[],
			[
				Component(
					"Mixed",
					Field("charge", ItemComponentFieldKind.Float, floatValue: 0.5f),
					Field("rounds", ItemComponentFieldKind.Int, intValue: 3),
					Field("lit", ItemComponentFieldKind.Bool, boolValue: true),
					Field("label", ItemComponentFieldKind.String, stringValue: "x"),
					Field("tags", ItemComponentFieldKind.StringList, stringList: ["a", "b"]),
					Field("mode", ItemComponentFieldKind.Enum, intValue: 2)),
			]));

		var text = ItemDataRenderer.One(item, FakeItemState.Describe(item.Data), [item]);

		Assert.Contains("Components:{Mixed:{charge:0.5f,rounds:3,lit:true", text);
		Assert.Contains("tags:[", text);
		Assert.Contains("mode:2}}", text);
	}

	[Fact]
	public void One_QuotesStringValuesAndStringListElements()
	{
		var item = Item(4101, ItemLocation.Carried(new ActorId(Owner)), new ItemData(
			1f,
			false,
			0,
			[],
			[Component(
				"Mixed",
				Field("label", ItemComponentFieldKind.String, stringValue: "x"),
				Field("tags", ItemComponentFieldKind.StringList, stringList: ["a", "b"]))]));

		var text = ItemDataRenderer.One(item, FakeItemState.Describe(item.Data), [item]);

		Assert.Contains(SnbtWriter.Write(new SnbtString("x")), text);
		Assert.Contains(SnbtWriter.Write(new SnbtList([new SnbtString("a"), new SnbtString("b")])), text);
	}

	[Fact]
	public void One_WithAComponentThatCarriesNoSimpleField_RendersAnEmptyCompound()
	{
		var item = Item(4101, ItemLocation.Carried(new ActorId(Owner)), new ItemData(
			1f, false, 0, [], [Component("WaterContainerItem")]));

		Assert.Contains("Components:{WaterContainerItem:{}}", ItemDataRenderer.One(item, FakeItemState.Describe(item.Data), [item]));
	}

	[Fact]
	public void One_NamesItsContainerContentsById()
	{
		var parent = Item(4101, ItemLocation.Carried(new ActorId(Owner)));
		var child = Item(4201, ItemLocation.Contained(new ActorId(Owner), 4101));
		var other = Item(4301, ItemLocation.Contained(new ActorId(Owner), 9999));

		var text = ItemDataRenderer.One(parent, FakeItemState.Describe(parent.Data), [parent, child, other]);

		Assert.Contains("contents (1): #4201 backpack", text);
		Assert.DoesNotContain("#4301", text);
	}

	[Theory]
	[InlineData(ItemLocationKind.World)]
	[InlineData(ItemLocationKind.Applied)]
	[InlineData(ItemLocationKind.Contained)]
	[InlineData(ItemLocationKind.Terminal)]
	public void One_NamesEveryLocationFamily(ItemLocationKind kind)
	{
		var location = kind switch
		{
			ItemLocationKind.World => ItemLocation.World(12.25f, -4.5f),
			ItemLocationKind.Applied => ItemLocation.Applied(new ActorId(Owner), 2),
			ItemLocationKind.Contained => ItemLocation.Contained(new ActorId(Owner), 4100),
			_ => ItemLocation.Terminal(),
		};
		var item = Item(4101, location);

		var text = ItemDataRenderer.One(item, FakeItemState.Describe(item.Data), []);

		var expected = kind switch
		{
			ItemLocationKind.World => "world (12.25, -4.5)",
			ItemLocationKind.Applied => "applied to 1001, limb 2",
			ItemLocationKind.Contained => "inside #4100",
			_ => "terminal (destroyed or consumed)",
		};
		Assert.Contains(expected, text);
	}

	[Fact]
	public void List_RendersOneLinePerItemWithItsSlotAndDocument()
	{
		var carried = Item(4101, ItemLocation.Carried(new ActorId(Owner)), new ItemData(1f, false, 0, [], []));
		var applied = Item(4102, ItemLocation.Applied(new ActorId(Owner), 3));

		var text = ItemDataRenderer.List(
			Owner,
			[carried, applied],
			[FakeItemState.Describe(carried.Data), FakeItemState.Describe(applied.Data)]);

		var lines = text.Split('\n');
		Assert.Equal("Items recorded for 1001 (2):", lines[0]);
		Assert.Contains("#4101 backpack [slot 0]", lines[1]);
		Assert.Contains("Condition:1f", lines[1]);
		Assert.Contains("#4102 backpack [limb 3]", lines[2]);
	}

	[Fact]
	public void PathLine_NamesTheItemThePathAndTheValue()
	{
		var item = Item(4101, ItemLocation.Carried(new ActorId(Owner)));
		Assert.True(NbtPathParser.TryParse("Liquids[0].Amount", out var path, out _));

		Assert.Equal(
			"#4101 backpack: Liquids[0].Amount = 250f",
			ItemDataRenderer.PathLine(item, path, new SnbtFloat(250f, Single: true)));
	}

	[Fact]
	public void PathFailure_NamesThePathAndTheReason()
	{
		var item = Item(4101, ItemLocation.Carried(new ActorId(Owner)));
		Assert.True(NbtPathParser.TryParse("Liquids[3]", out var path, out _));

		Assert.Equal(
			"#4101 backpack: Liquids[3] — 'Liquids' has 1 element(s), so [3] is out of range",
			ItemDataRenderer.PathFailure(item, path, "'Liquids' has 1 element(s), so [3] is out of range"));
	}

	/// <summary>
	/// The write's answer: the item, one clause per changed path built by the caller, and the
	/// consequence the adapter attached (a condition at 0 is destroyable by the game's rule).
	/// </summary>
	[Fact]
	public void Wrote_NamesTheItemTheClausesAndTheNote()
	{
		var item = new CharacterItemMsg { InstanceId = 4101, ItemId = "backpack" };

		var text = ItemDataRenderer.Wrote(item, ["Condition 0.5f -> 0f"], awaitingHost: false, "At 0 the item's own rule may destroy it.");

		Assert.Equal(
			"#4101 backpack: Condition 0.5f -> 0f — written and reported as this client's own item state. At 0 the item's own rule may destroy it.",
			text);
	}

	[Fact]
	public void Wrote_WhenAHostWillJudgeIt_IsProvisional()
	{
		var item = new CharacterItemMsg { InstanceId = 4101, ItemId = "backpack" };

		var text = ItemDataRenderer.Wrote(item, ["Condition 0.5f -> 0.25f"], awaitingHost: true, null);

		Assert.Contains("reported to the host", text);
		Assert.DoesNotContain("may destroy it", text);
	}

	private static ItemState Item(ulong instanceId, ItemLocation location, ItemData? data = null) =>
		new(new ItemIdentity(instanceId, "backpack"), 7, location)
		{
			Data = data ?? ItemData.Empty,
		};

	private static ItemComponentState Component(string typeName, params ItemComponentField[] fields) =>
		new(typeName, fields);

	private static ItemComponentField Field(
		string name,
		ItemComponentFieldKind kind,
		float floatValue = 0f,
		int intValue = 0,
		bool boolValue = false,
		string stringValue = "",
		IReadOnlyList<string>? stringList = null) =>
		new(name, kind, floatValue, intValue, boolValue, stringValue, stringList ?? []);
}
