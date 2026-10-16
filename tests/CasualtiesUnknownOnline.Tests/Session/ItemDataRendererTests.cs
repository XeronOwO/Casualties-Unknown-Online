using System.Collections.Generic;
using CasualtiesUnknownOnline.GameState;
using CasualtiesUnknownOnline.GameState.Domains.Items;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using CasualtiesUnknownOnline.Runtime.Session.Commands;
using CasualtiesUnknownOnline.Runtime.Session.Items;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Session;

/// <summary>
/// What the item-data verb prints: every save-shaped property, each component
/// field kind, and each location family. Pure — the kernel and the session are
/// not involved — because the wording is the contract the operator reads and a
/// later write has to name.
/// </summary>
public class ItemDataRendererTests
{
	private const ulong Owner = 1001;

	[Fact]
	public void One_RendersEverySaveShapedProperty()
	{
		var item = Item(4101, ItemLocation.Carried(new ActorId(Owner)), new ItemData(
			0.8f,
			true,
			1,
			[new ItemLiquidStack("water", 250f)],
			[Component("BatteryItem", Field("charge", ItemComponentFieldKind.Float, floatValue: 0.75f))]));

		var text = ItemDataRenderer.One(item, [item]);

		Assert.Contains("#4101 backpack [carried by 1001, slot 1] revision 7", text);
		Assert.Contains("condition 0.8", text);
		Assert.Contains("favourited yes", text);
		Assert.Contains("liquid water 250 ml", text);
		Assert.Contains("component BatteryItem: float charge=0.75", text);
	}

	[Fact]
	public void One_RendersEachFieldKindAsItsOwnValue()
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

		var text = ItemDataRenderer.One(item, [item]);

		Assert.Contains("component Mixed: float charge=0.5, int rounds=3, bool lit=true", text);
		Assert.Contains("string label=\"x\"", text);
		Assert.Contains("strings tags=[a, b]", text);
		Assert.Contains("enum mode=2", text);
	}

	[Fact]
	public void One_WithAComponentThatCarriesNoSimpleField_SaysSo()
	{
		var item = Item(4101, ItemLocation.Carried(new ActorId(Owner)), new ItemData(
			1f, false, 0, [], [Component("WaterContainerItem")]));

		Assert.Contains("component WaterContainerItem: (no simple-typed field)", ItemDataRenderer.One(item, [item]));
	}

	[Fact]
	public void One_NamesItsContainerContentsById()
	{
		var parent = Item(4101, ItemLocation.Carried(new ActorId(Owner)));
		var child = Item(4201, ItemLocation.Contained(new ActorId(Owner), 4101));
		var other = Item(4301, ItemLocation.Contained(new ActorId(Owner), 9999));

		var text = ItemDataRenderer.One(parent, [parent, child, other]);

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

		var text = ItemDataRenderer.One(Item(4101, location), []);

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
	public void List_RendersOneLinePerItemWithItsSlot()
	{
		var carried = Item(4101, ItemLocation.Carried(new ActorId(Owner)), new ItemData(1f, false, 0, [], []));
		var applied = Item(4102, ItemLocation.Applied(new ActorId(Owner), 3));

		var text = ItemDataRenderer.List(Owner, [carried, applied], [carried, applied]);

		var lines = text.Split('\n');
		Assert.Equal("Items recorded for 1001 (2):", lines[0]);
		Assert.Contains("#4101 backpack [slot 0]", lines[1]);
		Assert.Contains("#4102 backpack [limb 3]", lines[2]);
	}

	[Fact]
	public void List_CountsTheContentsOfAContainer()
	{
		var parent = Item(4101, ItemLocation.Carried(new ActorId(Owner)));
		var child = Item(4201, ItemLocation.Contained(new ActorId(Owner), 4101));

		Assert.Contains("content(s)", ItemDataRenderer.List(Owner, [parent], [parent, child]));
	}

	/// <summary>
	/// The write's answer: the item, the property in the words the read half names it
	/// with, and the value it moved from and to. The two values are formatted by the
	/// read half's own helpers, so what an operator wrote reads back in the words
	/// they read it in.
	/// </summary>
	[Fact]
	public void Wrote_NamesTheItemThePropertyAndBothValues()
	{
		var item = new CharacterItemMsg { InstanceId = 4101, ItemId = "backpack" };
		Assert.True(ItemDataWriteParser.TryParse(["slot", "1", "condition", "0.25"], out var write, out _));

		var text = ItemDataRenderer.WroteMany(item, [write], ["0.5 -> 0.25"], awaitingHost: false);

		Assert.Equal("#4101 backpack: condition 0.5 -> 0.25 — written and reported as this client's own item state.", text);
	}

	[Theory]
	[InlineData("condition 0.25", "condition")]
	[InlineData("favourited yes", "favourited")]
	[InlineData("liquid water 500", "liquid water")]
	[InlineData("component BatteryItem charge 0.9", "component BatteryItem charge")]
	public void Wrote_LabelsEachPropertyTheReadHalfPrintsItWith(string writeText, string label)
	{
		var item = new CharacterItemMsg { InstanceId = 4101, ItemId = "backpack" };
		Assert.True(ItemDataWriteParser.TryParse($"id 4101 {writeText}".Split(' '), out var write, out _));

		Assert.StartsWith($"#4101 backpack: {label} ", ItemDataRenderer.WroteMany(item, [write], ["before -> after"], awaitingHost: false));
	}

	/// <summary>The write half formats the LOCAL capture's own field shape — the kind the capture tagged the field with, which is what the write read the value as.</summary>
	[Fact]
	public void FieldValue_FormatsEveryCapturedKind()
	{
		Assert.Equal("0.5", ItemDataRenderer.FieldValue(new ComponentFieldMsg { Kind = SaveableFieldKind.Float, FloatValue = 0.5f }));
		Assert.Equal("3", ItemDataRenderer.FieldValue(new ComponentFieldMsg { Kind = SaveableFieldKind.Int, IntValue = 3 }));
		Assert.Equal("true", ItemDataRenderer.FieldValue(new ComponentFieldMsg { Kind = SaveableFieldKind.Bool, BoolValue = true }));
		Assert.Equal("\"x\"", ItemDataRenderer.FieldValue(new ComponentFieldMsg { Kind = SaveableFieldKind.String, StringValue = "x" }));
		Assert.Equal("[a, b]", ItemDataRenderer.FieldValue(new ComponentFieldMsg { Kind = SaveableFieldKind.StringList, StringList = ["a", "b"] }));
		Assert.Equal("2", ItemDataRenderer.FieldValue(new ComponentFieldMsg { Kind = SaveableFieldKind.Enum, IntValue = 2 }));
	}

	/// <summary>
	/// A condition written to zero is the one write whose consequence the operator has
	/// to be told: the item's own rule destroys what reaches zero, so the answer says
	/// the item may be gone — the value is still written as asked.
	/// </summary>
	[Fact]
	public void Wrote_AtZeroCondition_SaysTheItemMayBeDestroyed()
	{
		var item = new CharacterItemMsg { InstanceId = 4101, ItemId = "backpack" };
		Assert.True(ItemDataWriteParser.TryParse(["slot", "1", "condition", "0"], out var write, out _));

		Assert.Contains("may destroy it", ItemDataRenderer.WroteMany(item, [write], ["0.5 -> 0"], awaitingHost: false));
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
