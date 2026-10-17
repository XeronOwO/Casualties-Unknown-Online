using System;
using System.Collections.Generic;
using System.Linq;
using CasualtiesUnknownOnline.GameState.Domains.Items;
using CasualtiesUnknownOnline.Runtime.Data;
using CasualtiesUnknownOnline.Runtime.GameAdapter;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using CasualtiesUnknownOnline.Runtime.Session.Items;

namespace CasualtiesUnknownOnline.Tests.Fakes;

/// <summary>
/// The Game Adapter's item state document, as a test's own fixture: the same four keys, the
/// same component shapes and the same refusals the real <c>ItemStateDocument</c> produces,
/// over the three definitions <see cref="FakeItemGrantSource"/> serves. It exists because a
/// test host has no game assembly: the COMMAND's half (parse, path, edit, answer) is what the
/// console suites prove, while the adapter's own binding is the acceptance run's.
///
/// <para>
/// The fixture models the two game facts the state language must not lose: a waterbottle is a
/// container with 500 ml of capacity, and a battery carries the <c>BatteryItem</c> component
/// with a float <c>charge</c> and a bool <c>on</c>. Everything else is refused by name, so a
/// suite can tell "the command refused" from "the fake accepted anything".
/// </para>
/// </summary>
internal static class FakeItemState
{
	internal const string WaterBottle = "waterbottle";

	internal const string LargeBattery = "largebattery";

	internal const string Bandage = "bandage";

	internal const string BatteryItem = "BatteryItem";

	internal const string Charge = "charge";

	internal const string On = "on";

	internal const float WaterBottleCapacity = 500f;

	private static readonly string[] LiquidIds = ["water", "blood"];

	/// <summary>The prefab capture of one definition, or null when this build does not know it. The waterbottle's capture is EMPTY: the prefab is not an instantiated item.</summary>
	internal static CharacterItemMsg? Definition(string definitionId) => definitionId switch
	{
		WaterBottle => new CharacterItemMsg { ItemId = WaterBottle, Condition = 1f, SlotIndex = -1 },
		LargeBattery => new CharacterItemMsg
		{
			ItemId = LargeBattery,
			Condition = 1f,
			SlotIndex = -1,
			Components = [BatteryComponent(charge: 0.5f, on: false)],
		},
		Bandage => new CharacterItemMsg { ItemId = Bandage, Condition = 1f, SlotIndex = -1 },
		_ => null,
	};

	/// <summary>The document for one local capture.</summary>
	internal static SnbtValue Describe(CharacterItemMsg capture) =>
		Describe(capture.Condition, capture.Favourited, capture.Liquids, capture.Components);

	/// <summary>The document for one kernel row.</summary>
	internal static SnbtValue Describe(ItemData state) =>
		Describe(
			state.Condition,
			state.Favourited,
			[.. state.Liquids.Select(liquid => new LiquidStackMsg { LiquidId = liquid.LiquidId, Amount = liquid.Amount })],
			[.. state.Components.Select(component => new ComponentStateMsg
			{
				TypeName = component.TypeName,
				Fields = [.. component.Fields.Select(field => new ComponentFieldMsg
				{
					Name = field.Name,
					Kind = (int)field.Kind,
					FloatValue = field.FloatValue,
					IntValue = field.IntValue,
					BoolValue = field.BoolValue,
					StringValue = field.StringValue,
					StringList = [.. field.StringList],
				})],
			})]);

	/// <summary>The schema for one kernel row, in the shape the console completion walks.</summary>
	internal static ItemStateSchema Schema(ItemData state) =>
		Schema(new CharacterItemMsg
		{
			ItemId = state.Components.Count > 0 ? LargeBattery : Bandage,
			Condition = state.Condition,
			Favourited = state.Favourited,
			Components = [.. state.Components.Select(component => new ComponentStateMsg
			{
				TypeName = component.TypeName,
				Fields = [.. component.Fields.Select(field => new ComponentFieldMsg
				{
					Name = field.Name,
					Kind = (int)field.Kind,
					FloatValue = field.FloatValue,
					IntValue = field.IntValue,
					BoolValue = field.BoolValue,
					StringValue = field.StringValue,
					StringList = [.. field.StringList],
				})],
			})],
		});

	/// <summary>The schema for one capture, in the shape the console completion walks.</summary>
	internal static ItemStateSchema Schema(CharacterItemMsg capture) =>
		new(new ItemStateCompoundShape([
			new ItemStateKeyShape("Condition", new ItemStateScalarShape(ItemStateValueKind.Number), "0 or more"),
			new ItemStateKeyShape("Favourited", new ItemStateScalarShape(ItemStateValueKind.Bool)),
			new ItemStateKeyShape("Liquids", new ItemStateListShape(new ItemStateCompoundShape([
				new ItemStateKeyShape("LiquidId", new ItemStateScalarShape(ItemStateValueKind.String, LiquidIds), "the game's own liquid id"),
				new ItemStateKeyShape("Amount", new ItemStateScalarShape(ItemStateValueKind.Number), "millilitres"),
			]))),
			new ItemStateKeyShape("Components", new ItemStateCompoundShape(
				[.. capture.Components.Select(component => new ItemStateKeyShape(
					component.TypeName,
					new ItemStateCompoundShape([.. component.Fields.Select(FieldShape)]),
					$"the {component.TypeName} component"))])),
		]));

	/// <summary>The schema for a definition, or null when this build does not know it.</summary>
	internal static ItemStateSchema? DefinitionSchema(string definitionId) =>
		Definition(definitionId) is { } capture ? Schema(capture) : null;

	/// <summary>
	/// Binds a document to a capture with the fixture's own schema and legality: all four keys,
	/// every captured component and field, one stack per liquid, at most the container's
	/// capacity, and a non-empty liquid list only on a container.
	/// </summary>
	internal static bool TryBind(CharacterItemMsg capture, SnbtValue document, out CharacterItemMsg bound, out string? refusal)
	{
		bound = capture;
		refusal = null;
		if (document is not SnbtCompound compound)
		{
			refusal = $"an item's state is a compound — got {document.Kind}.";
			return false;
		}

		foreach (var entry in compound.Entries)
		{
			if (entry.Key is not ("Condition" or "Favourited" or "Liquids" or "Components"))
			{
				refusal = $"'{entry.Key}' is not part of an item's state — the state carries Condition, Favourited, Liquids and Components.";
				return false;
			}
		}

		foreach (var key in new[] { "Condition", "Favourited", "Liquids", "Components" })
		{
			if (!compound.Has(key))
			{
				refusal = $"the state is missing '{key}'.";
				return false;
			}
		}

		if (!compound.TryGet("Condition", out var conditionValue) || !TryNumber(conditionValue, out var condition) || condition < 0f)
		{
			refusal = "Condition takes a number 0 or more.";
			return false;
		}

		if (!compound.TryGet("Favourited", out var favouritedValue) || !TryFlag(favouritedValue, out var favourited))
		{
			refusal = "Favourited takes true or false.";
			return false;
		}

		if (!TryBindLiquids(capture, compound, out var liquids, out refusal))
		{
			return false;
		}

		if (!TryBindComponents(capture, compound, out var components, out refusal))
		{
			return false;
		}

		bound = new CharacterItemMsg
		{
			InstanceId = capture.InstanceId,
			ItemId = capture.ItemId,
			SlotIndex = capture.SlotIndex,
			Condition = condition,
			Favourited = favourited,
			Liquids = liquids,
			Components = components,
			Contents = capture.Contents,
		};
		return true;
	}

	private static bool TryBindLiquids(CharacterItemMsg capture, SnbtCompound compound, out List<LiquidStackMsg> liquids, out string? refusal)
	{
		liquids = [];
		refusal = null;
		if (!compound.TryGet("Liquids", out var value) || value is not SnbtList list)
		{
			refusal = "Liquids takes a list of stacks.";
			return false;
		}

		var seen = new HashSet<string>(StringComparer.Ordinal);
		var total = 0f;
		for (var i = 0; i < list.Items.Count; i++)
		{
			if (list.Items[i] is not SnbtCompound stack
				|| !stack.TryGet("LiquidId", out var idValue) || idValue is not SnbtString id
				|| !stack.TryGet("Amount", out var amountValue) || !TryNumber(amountValue, out var amount) || amount < 0f)
			{
				refusal = $"Liquids[{i}] is not {{LiquidId:'water',Amount:500}}.";
				return false;
			}

			if (amount == 0f)
			{
				continue;
			}

			if (!seen.Add(id.Value))
			{
				refusal = $"Liquids[{i}] names '{id.Value}' twice.";
				return false;
			}

			total += amount;
			liquids.Add(new LiquidStackMsg { LiquidId = id.Value, Amount = amount });
		}

		if (liquids.Count == 0)
		{
			return true;
		}

		if (!string.Equals(capture.ItemId, WaterBottle, StringComparison.Ordinal) && capture.Liquids.Count == 0)
		{
			refusal = "this item has no liquid container, so its state cannot carry Liquids.";
			return false;
		}

		if (total > WaterBottleCapacity)
		{
			refusal = $"the container holds {WaterBottleCapacity} ml and the state names {total} ml.";
			return false;
		}

		return true;
	}

	private static bool TryBindComponents(CharacterItemMsg capture, SnbtCompound compound, out List<ComponentStateMsg> components, out string? refusal)
	{
		components = [];
		refusal = null;
		if (!compound.TryGet("Components", out var value) || value is not SnbtCompound document)
		{
			refusal = "Components takes a compound keyed by component type.";
			return false;
		}

		foreach (var entry in document.Entries)
		{
			if (capture.Components.All(component => !string.Equals(component.TypeName, entry.Key, StringComparison.Ordinal)))
			{
				refusal = $"the item carries no '{entry.Key}' component.";
				return false;
			}
		}

		foreach (var captured in capture.Components)
		{
			if (!document.TryGet(captured.TypeName, out var componentValue) || componentValue is not SnbtCompound component)
			{
				refusal = $"the item carries '{captured.TypeName}' and its state cannot drop a component.";
				return false;
			}

			foreach (var entry in component.Entries)
			{
				if (captured.Fields.All(field => !string.Equals(field.Name, entry.Key, StringComparison.Ordinal)))
				{
					refusal = $"component '{captured.TypeName}' has no field '{entry.Key}'.";
					return false;
				}
			}

			var fields = new List<ComponentFieldMsg>(captured.Fields.Count);
			foreach (var field in captured.Fields)
			{
				if (!component.TryGet(field.Name, out var fieldValue) || !TryField(field, fieldValue, out var bound))
				{
					refusal = $"{captured.TypeName}.{field.Name} is missing or takes the wrong kind.";
					return false;
				}

				fields.Add(bound);
			}

			components.Add(new ComponentStateMsg { TypeName = captured.TypeName, Fields = fields });
		}

		return true;
	}

	private static bool TryField(ComponentFieldMsg field, SnbtValue value, out ComponentFieldMsg bound)
	{
		bound = new ComponentFieldMsg { Name = field.Name, Kind = field.Kind };
		switch (field.Kind)
		{
			case SaveableFieldKind.Float when TryNumber(value, out var number):
				bound.FloatValue = number;
				return true;
			case SaveableFieldKind.Int when value is SnbtInteger integer:
				bound.IntValue = (int)integer.Value;
				return true;
			case SaveableFieldKind.Bool when TryFlag(value, out var flag):
				bound.BoolValue = flag;
				return true;
			case SaveableFieldKind.String when value is SnbtString text:
				bound.StringValue = text.Value;
				return true;
			case SaveableFieldKind.StringList when value is SnbtList list && list.Items.All(item => item is SnbtString):
				bound.StringList = [.. list.Items.Select(item => ((SnbtString)item).Value)];
				return true;
			default:
				return false;
		}
	}

	private static SnbtValue Describe(float condition, bool favourited, IReadOnlyList<LiquidStackMsg> liquids, IReadOnlyList<ComponentStateMsg> components) =>
		new SnbtCompound([
			new SnbtEntry("Condition", new SnbtFloat(condition, Single: true)),
			new SnbtEntry("Favourited", new SnbtBool(favourited)),
			new SnbtEntry("Liquids", new SnbtList([.. liquids.Select(liquid => (SnbtValue)new SnbtCompound([
				new SnbtEntry("LiquidId", new SnbtString(liquid.LiquidId)),
				new SnbtEntry("Amount", new SnbtFloat(liquid.Amount, Single: true)),
			]))])),
			new SnbtEntry("Components", new SnbtCompound([.. components.Select(component => new SnbtEntry(
				component.TypeName,
				new SnbtCompound([.. component.Fields.Select(field => new SnbtEntry(field.Name, FieldValue(field)))])))])),
		]);

	private static SnbtValue FieldValue(ComponentFieldMsg field) => field.Kind switch
	{
		SaveableFieldKind.Float => new SnbtFloat(field.FloatValue, Single: true),
		SaveableFieldKind.Int or SaveableFieldKind.Enum => new SnbtInteger(field.IntValue),
		SaveableFieldKind.Bool => new SnbtBool(field.BoolValue),
		SaveableFieldKind.String => new SnbtString(field.StringValue),
		SaveableFieldKind.StringList => new SnbtList([.. field.StringList.Select(item => (SnbtValue)new SnbtString(item))]),
		_ => new SnbtString(""),
	};

	private static ItemStateKeyShape FieldShape(ComponentFieldMsg field) =>
		new(field.Name, field.Kind switch
		{
			SaveableFieldKind.Bool => new ItemStateScalarShape(ItemStateValueKind.Bool),
			SaveableFieldKind.String => new ItemStateScalarShape(ItemStateValueKind.String),
			SaveableFieldKind.StringList => new ItemStateListShape(new ItemStateScalarShape(ItemStateValueKind.String)),
			_ => new ItemStateScalarShape(ItemStateValueKind.Number),
		}, "a captured field");

	private static ComponentStateMsg BatteryComponent(float charge, bool on) => new()
	{
		TypeName = BatteryItem,
		Fields =
		[
			new ComponentFieldMsg { Name = Charge, Kind = SaveableFieldKind.Float, FloatValue = charge },
			new ComponentFieldMsg { Name = On, Kind = SaveableFieldKind.Bool, BoolValue = on },
		],
	};

	private static bool TryNumber(SnbtValue value, out float number)
	{
		switch (value)
		{
			case SnbtInteger integer when integer.Value is >= int.MinValue and <= int.MaxValue:
				number = (int)integer.Value;
				return true;
			case SnbtFloat single:
				number = (float)single.Value;
				return true;
			default:
				number = 0f;
				return false;
		}
	}

	private static bool TryFlag(SnbtValue value, out bool flag)
	{
		switch (value)
		{
			case SnbtBool boolean:
				flag = boolean.Value;
				return true;
			case SnbtInteger integer when integer.Value is 0 or 1:
				flag = integer.Value == 1;
				return true;
			default:
				flag = false;
				return false;
		}
	}
}

/// <summary>The state language as a test's own registration: the fixture's projection and schema, no game assembly.</summary>
internal sealed class FakeItemStateLanguage : IItemStateLanguage
{
	internal static FakeItemStateLanguage Instance { get; } = new();

	public bool TryDescribe(ItemData state, out SnbtValue document)
	{
		document = FakeItemState.Describe(state);
		return true;
	}

	public bool TryDescribe(CharacterItemMsg capture, out SnbtValue document)
	{
		document = FakeItemState.Describe(capture);
		return true;
	}

	public ItemStateSchema? DescribeSchema(ItemData state) => FakeItemState.Schema(state);

	public ItemStateSchema? DescribeDefinitionSchema(string definitionId) => FakeItemState.DefinitionSchema(definitionId);
}
