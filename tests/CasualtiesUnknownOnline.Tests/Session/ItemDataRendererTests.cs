using System.Collections.Generic;
using CasualtiesUnknownOnline.GameState;
using CasualtiesUnknownOnline.GameState.Domains.Items;
using CasualtiesUnknownOnline.Runtime.Session.Commands;
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
