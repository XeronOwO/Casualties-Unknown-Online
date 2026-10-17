using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using CasualtiesUnknownOnline.GameState.Domains.Items;
using CasualtiesUnknownOnline.Runtime.Data;
using CasualtiesUnknownOnline.Runtime.GameAdapter;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using CasualtiesUnknownOnline.Runtime.Session.Items;

namespace CasualtiesUnknownOnline.GameAdapter.Items;

/// <summary>
/// The Game Adapter's item state document: the projection of a save-shaped item state to
/// the SNBT tree, the schema for completion, and the binding of a tree back to a capture
/// with the game's own legality.
///
/// <para>
/// This is where the SCHEMA lives (decision 264): the four top-level keys, the liquid
/// stack's shape, the component type names and their captured field names and kinds, the
/// game's liquid ids, and the two rules only this layer can read — whether the item has a
/// <c>WaterContainerItem</c> at all and how much its <c>Capacity</c> holds. The Runtime
/// never learns any of it; it parses the text, resolves the path and prints the answer.
/// </para>
///
/// <para>
/// Binding is validate-then-write: every key, component, field and value is checked
/// against the capture and the live item BEFORE the out-parameter is built, so a refused
/// document leaves both the item and the caller's tree untouched. The document is
/// COMPLETE — every top-level key and every captured field is required — which is what
/// makes a removal that would strip a required key a named refusal instead of a silent
/// default.
/// </para>
/// </summary>
internal static class ItemStateDocument
{
	internal const string ConditionKey = "Condition";

	internal const string FavouritedKey = "Favourited";

	internal const string LiquidsKey = "Liquids";

	internal const string ComponentsKey = "Components";

	internal const string LiquidIdKey = "LiquidId";

	internal const string AmountKey = "Amount";

	/// <summary>The four keys an item's state carries, in the order the schema and every refusal list them.</summary>
	internal static readonly string[] StateKeys = [ConditionKey, FavouritedKey, LiquidsKey, ComponentsKey];

	/// <summary>The document for one recorded (kernel) state.</summary>
	internal static SnbtValue Describe(ItemData state) =>
		Describe(new Snapshot(
			state.Condition,
			state.Favourited,
			[.. state.Liquids.Select(liquid => new LiquidSnapshot(liquid.LiquidId, liquid.Amount))],
			[.. state.Components.Select(Component)]));

	/// <summary>The document for one local capture.</summary>
	internal static SnbtValue Describe(CharacterItemMsg capture) => Describe(SnapshotOf(capture));

	/// <summary>The completion schema for one recorded state.</summary>
	internal static ItemStateSchema Schema(ItemData state) =>
		Schema(new Snapshot(
			state.Condition,
			state.Favourited,
			[.. state.Liquids.Select(liquid => new LiquidSnapshot(liquid.LiquidId, liquid.Amount))],
			[.. state.Components.Select(Component)]));

	/// <summary>The completion schema for one local capture.</summary>
	internal static ItemStateSchema Schema(CharacterItemMsg capture) => Schema(SnapshotOf(capture));

	/// <summary>The schema of a definition, read from its prefab; null when this build does not know it.</summary>
	internal static ItemStateSchema? DefinitionSchema(string definitionId) =>
		ItemDefinitionTable.TryResolve(definitionId, out var template, out _) && template is not null
			? Schema(ItemStateCodec.CaptureItem(template, slotIndex: -1))
			: null;

	/// <summary>
	/// Binds a document to a capture against <paramref name="source"/> (the live item, or
	/// the prefab for a give) and refuses BY NAME every way the document is not a state that
	/// item can carry. Nothing is mutated: the caller applies <paramref name="bound"/> through
	/// the scene's own write (or delivers it as the created fact).
	/// </summary>
	internal static bool TryBind(Item source, CharacterItemMsg capture, SnbtValue document, out CharacterItemMsg bound, out string? refusal)
	{
		bound = capture;
		refusal = null;
		if (document is not SnbtCompound compound)
		{
			refusal = $"an item's state is a compound, e.g. {{Condition:0.8,{FavouritedKey}:true,{LiquidsKey}:[],{ComponentsKey}:{{}}}} — got {document.Kind}.";
			return false;
		}

		foreach (var entry in compound.Entries)
		{
			if (!IsStateKey(entry.Key))
			{
				refusal = $"'{entry.Key}' is not part of an item's state — the state carries {string.Join(", ", StateKeys)}.";
				return false;
			}
		}

		foreach (var key in StateKeys)
		{
			if (!compound.Has(key))
			{
				refusal = $"the state is missing '{key}' — an item's state carries {string.Join(", ", StateKeys)}; /data get prints the shape to start from.";
				return false;
			}
		}

		var snapshot = SnapshotOf(capture);
		if (!compound.TryGet(ConditionKey, out var conditionValue)
			|| !TryNumber(conditionValue, out var condition)
			|| condition < 0f)
		{
			refusal = $"{ConditionKey} takes a number 0 or more — got {DescribeValue(compound, ConditionKey)}.";
			return false;
		}

		if (!compound.TryGet(FavouritedKey, out var favouritedValue) || !TryFlag(favouritedValue, out var favourited))
		{
			refusal = $"{FavouritedKey} takes true or false — got {DescribeValue(compound, FavouritedKey)}.";
			return false;
		}

		if (!TryBindLiquids(source, compound, out var liquids, out var liquidTotal, out var capacity, out refusal))
		{
			return false;
		}

		if (!TryBindComponents(snapshot, compound, out var components, out refusal))
		{
			return false;
		}

		// A container's condition is its fill ratio in the game (WaterContainerItem.UpdateCondition),
		// so a state that names liquids binds the ratio rather than the requested scalar: the next
		// native liquid operation would overwrite anything else, and the created/held row would
		// disagree with the displayed fill until the next report.
		if (liquidTotal > 0f && capacity > 0f)
		{
			condition = Math.Min(1f, liquidTotal / capacity);
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

	/// <summary>
	/// The liquid half: every stack is <c>{LiquidId:"…",Amount:…}</c>, one stack per liquid,
	/// a zero amount drops the stack, and the container's own capacity decides what fits. A
	/// non-empty list on an item with no <c>WaterContainerItem</c> is refused by name — the
	/// capture cannot tell "no container" from "empty container", and this layer can.
	/// </summary>
	private static bool TryBindLiquids(
		Item source,
		SnbtCompound compound,
		out List<LiquidStackMsg> liquids,
		out float total,
		out float capacity,
		out string? refusal)
	{
		liquids = [];
		total = 0f;
		capacity = 0f;
		refusal = null;
		if (!compound.TryGet(LiquidsKey, out var value) || value is not SnbtList list)
		{
			refusal = $"{LiquidsKey} takes a list of stacks, e.g. [{{LiquidId:'water',{AmountKey}:500}}].";
			return false;
		}

		var seen = new HashSet<string>(StringComparer.Ordinal);
		for (var i = 0; i < list.Items.Count; i++)
		{
			var path = $"{LiquidsKey}[{i}]";
			if (list.Items[i] is not SnbtCompound stack)
			{
				refusal = $"{path} is {list.Items[i].Kind}, not a stack compound — a stack is {{LiquidId:'water',{AmountKey}:500}}.";
				return false;
			}

			foreach (var entry in stack.Entries)
			{
				if (entry.Key is not (LiquidIdKey or AmountKey))
				{
					refusal = $"{path} carries '{entry.Key}' — a stack carries {LiquidIdKey} and {AmountKey}.";
					return false;
				}
			}

			if (!stack.TryGet(LiquidIdKey, out var idValue) || idValue is not SnbtString liquidId || liquidId.Value.Length == 0)
			{
				refusal = $"{path} needs {LiquidIdKey} as a string, e.g. {LiquidIdKey}:'water'.";
				return false;
			}

			if (!stack.TryGet(AmountKey, out var amountValue) || !TryNumber(amountValue, out var amount) || amount < 0f)
			{
				refusal = $"{path}.{AmountKey} takes a number 0 or more (millilitres) — 0 drops the stack.";
				return false;
			}

			if (amount == 0f)
			{
				continue;
			}

			if (!seen.Add(liquidId.Value))
			{
				refusal = $"{path} names the liquid '{liquidId.Value}' twice — a container holds one stack per liquid.";
				return false;
			}

			total += amount;
			liquids.Add(new LiquidStackMsg { LiquidId = liquidId.Value, Amount = amount });
		}

		if (liquids.Count == 0)
		{
			return true;
		}

		if (source.GetComponent<WaterContainerItem>() == null) // Unity object — ==
		{
			refusal = $"this item has no liquid container, so its state cannot carry {LiquidsKey}.";
			return false;
		}

		// The capacity comes from the ITEM's own stats, not from WaterContainerItem.Capacity: that
		// property reads a private field assigned in Awake, and the give path binds a PREFAB ASSET
		// where Awake never ran (the same prefab-is-not-an-instance fact CaptureLiquids handles).
		capacity = (source.Stats as LiquidItemInfo)?.capacity ?? 0f;
		if (capacity <= 0f)
		{
			refusal = "this item's own data names no liquid capacity, so its state cannot carry liquids.";
			return false;
		}

		if (total > capacity)
		{
			refusal = $"the container holds {Number(capacity)} ml and the state names {Number(total)} ml.";
			return false;
		}

		if (Liquids.Registry is { } registry)
		{
			foreach (var liquid in liquids)
			{
				if (!registry.ContainsKey(liquid.LiquidId))
				{
					refusal = $"'{liquid.LiquidId}' is not one of the game's liquids — the state names a liquid the game does not know.";
					return false;
				}
			}
		}

		return true;
	}

	/// <summary>
	/// The component half: the document must name every component the item carries (a
	/// component cannot be dropped from a live item) and every field each component carries
	/// (its state is complete), and nothing it does not carry.
	/// </summary>
	private static bool TryBindComponents(Snapshot snapshot, SnbtCompound compound, out List<ComponentStateMsg> components, out string? refusal)
	{
		components = [];
		refusal = null;
		if (!compound.TryGet(ComponentsKey, out var value) || value is not SnbtCompound document)
		{
			refusal = $"{ComponentsKey} takes a compound keyed by component type, e.g. {{BatteryItem:{{charge:0.9}}}}.";
			return false;
		}

		foreach (var entry in document.Entries)
		{
			if (!snapshot.Components.Any(component => string.Equals(component.TypeName, entry.Key, StringComparison.Ordinal)))
			{
				refusal = $"the item carries no '{entry.Key}' component — it carries: {ComponentNames(snapshot)}.";
				return false;
			}
		}

		foreach (var captured in snapshot.Components)
		{
			if (!document.TryGet(captured.TypeName, out var componentValue) || componentValue is not SnbtCompound component)
			{
				refusal = $"the item carries '{captured.TypeName}' and its state cannot drop a component — name it too, or change one of its fields.";
				return false;
			}

			foreach (var entry in component.Entries)
			{
				if (!captured.Fields.Any(field => string.Equals(field.Name, entry.Key, StringComparison.Ordinal)))
				{
					refusal = $"component '{captured.TypeName}' has no field '{entry.Key}' — it carries: {FieldNames(captured)}.";
					return false;
				}
			}

			var fields = new List<ComponentFieldMsg>(captured.Fields.Count);
			foreach (var field in captured.Fields)
			{
				if (!component.TryGet(field.Name, out var fieldValue))
				{
					refusal = $"component '{captured.TypeName}' is missing field '{field.Name}' — a component's state carries every field it has; /data get prints the shape to start from.";
					return false;
				}

				if (!TryBindField(captured.TypeName, field, fieldValue, out var bound, out refusal))
				{
					return false;
				}

				fields.Add(bound);
			}

			components.Add(new ComponentStateMsg { TypeName = captured.TypeName, Fields = fields });
		}

		return true;
	}

	private static bool TryBindField(string componentType, FieldSnapshot field, SnbtValue value, out ComponentFieldMsg bound, out string? refusal)
	{
		bound = new ComponentFieldMsg { Name = field.Name, Kind = field.Kind };
		refusal = null;
		var path = $"{ComponentsKey}.{componentType}.{field.Name}";
		switch (field.Kind)
		{
			case SaveableFieldKind.Float when TryNumber(value, out var number):
				bound.FloatValue = number;
				return true;

			case SaveableFieldKind.Int when value is SnbtInteger integer && integer.Value is >= int.MinValue and <= int.MaxValue:
				bound.IntValue = (int)integer.Value;
				return true;

			case SaveableFieldKind.Enum when value is SnbtInteger underlying && underlying.Value is >= int.MinValue and <= int.MaxValue:
				bound.IntValue = (int)underlying.Value;
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
				refusal = $"{path} takes {KindLabel(field.Kind)}, not {value.Kind}.";
				return false;
		}
	}

	private static SnbtValue Describe(Snapshot state)
	{
		var liquids = new List<SnbtValue>(state.Liquids.Count);
		foreach (var liquid in state.Liquids)
		{
			liquids.Add(new SnbtCompound([
				new SnbtEntry(LiquidIdKey, new SnbtString(liquid.LiquidId)),
				new SnbtEntry(AmountKey, new SnbtFloat(liquid.Amount, Single: true)),
			]));
		}

		var components = new List<SnbtEntry>(state.Components.Count);
		foreach (var component in state.Components)
		{
			var fields = new List<SnbtEntry>(component.Fields.Count);
			foreach (var field in component.Fields)
			{
				fields.Add(new SnbtEntry(field.Name, FieldValue(field)));
			}

			components.Add(new SnbtEntry(component.TypeName, new SnbtCompound(fields)));
		}

		return new SnbtCompound([
			new SnbtEntry(ConditionKey, new SnbtFloat(state.Condition, Single: true)),
			new SnbtEntry(FavouritedKey, new SnbtBool(state.Favourited)),
			new SnbtEntry(LiquidsKey, new SnbtList(liquids)),
			new SnbtEntry(ComponentsKey, new SnbtCompound(components)),
		]);
	}

	private static ItemStateSchema Schema(Snapshot state)
	{
		var choices = LiquidChoices();
		var stack = new ItemStateCompoundShape([
			new ItemStateKeyShape(LiquidIdKey, new ItemStateScalarShape(ItemStateValueKind.String, choices), "the game's own liquid id"),
			new ItemStateKeyShape(AmountKey, new ItemStateScalarShape(ItemStateValueKind.Number), "millilitres; 0 drops the stack"),
		]);

		var componentKeys = new List<ItemStateKeyShape>(state.Components.Count);
		foreach (var component in state.Components)
		{
			var fields = new List<ItemStateKeyShape>(component.Fields.Count);
			foreach (var field in component.Fields)
			{
				fields.Add(new ItemStateKeyShape(field.Name, ShapeOf(field.Kind), KindLabel(field.Kind)));
			}

			componentKeys.Add(new ItemStateKeyShape(component.TypeName, new ItemStateCompoundShape(fields), $"the {component.TypeName} component"));
		}

		return new ItemStateSchema(new ItemStateCompoundShape([
			new ItemStateKeyShape(ConditionKey, new ItemStateScalarShape(ItemStateValueKind.Number), "0 or more; the game destroys an item at 0 by its own rule"),
			new ItemStateKeyShape(FavouritedKey, new ItemStateScalarShape(ItemStateValueKind.Bool)),
			new ItemStateKeyShape(LiquidsKey, new ItemStateListShape(stack), "the container's stacks; empty for a container holding none"),
			new ItemStateKeyShape(ComponentsKey, new ItemStateCompoundShape(componentKeys), "the captured component fields"),
		]));
	}

	private static SnbtValue FieldValue(FieldSnapshot field) => field.Kind switch
	{
		SaveableFieldKind.Float => new SnbtFloat(field.FloatValue, Single: true),
		SaveableFieldKind.Int or SaveableFieldKind.Enum => new SnbtInteger(field.IntValue),
		SaveableFieldKind.Bool => new SnbtBool(field.BoolValue),
		SaveableFieldKind.String => new SnbtString(field.StringValue),
		SaveableFieldKind.StringList => new SnbtList([.. field.StringList.Select(item => (SnbtValue)new SnbtString(item))]),
		_ => new SnbtString(""),
	};

	private static ItemStateShape ShapeOf(int kind) => kind switch
	{
		SaveableFieldKind.Bool => new ItemStateScalarShape(ItemStateValueKind.Bool),
		SaveableFieldKind.String => new ItemStateScalarShape(ItemStateValueKind.String),
		SaveableFieldKind.StringList => new ItemStateListShape(new ItemStateScalarShape(ItemStateValueKind.String)),
		_ => new ItemStateScalarShape(ItemStateValueKind.Number),
	};

	private static Snapshot SnapshotOf(CharacterItemMsg capture) =>
		new(
			capture.Condition,
			capture.Favourited,
			[.. capture.Liquids.Select(liquid => new LiquidSnapshot(liquid.LiquidId, liquid.Amount))],
			[.. capture.Components.Select(component => new ComponentSnapshot(
				component.TypeName,
				[.. component.Fields.Select(field => new FieldSnapshot(
					field.Name,
					field.Kind,
					field.FloatValue,
					field.IntValue,
					field.BoolValue,
					field.StringValue,
					[.. field.StringList]))]))]);

	private static ComponentSnapshot Component(ItemComponentState component) =>
		new(
			component.TypeName,
			[.. component.Fields.Select(field => new FieldSnapshot(
				field.Name,
				(int)field.Kind,
				field.FloatValue,
				field.IntValue,
				field.BoolValue,
				field.StringValue,
				[.. field.StringList]))]);

	private static bool TryNumber(SnbtValue value, out float number)
	{
		number = 0f;
		switch (value)
		{
			case SnbtInteger integer when integer.Value is >= int.MinValue and <= int.MaxValue:
				number = (int)integer.Value;
				return true;
			case SnbtFloat single:
				var narrowed = (float)single.Value;
				if (float.IsNaN(narrowed) || float.IsInfinity(narrowed))
				{
					return false;
				}

				number = narrowed;
				return true;
			default:
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

	private static IReadOnlyList<string> LiquidChoices() =>
		Liquids.Registry is { } registry
			? [.. registry.Keys.OrderBy(id => id, StringComparer.OrdinalIgnoreCase)]
			: [];

	private static string DescribeValue(SnbtCompound compound, string key) =>
		compound.TryGet(key, out var value) ? value.Kind : "nothing";

	private static string ComponentNames(Snapshot snapshot) =>
		snapshot.Components.Count == 0
			? "no captured component"
			: string.Join(", ", snapshot.Components.Select(component => component.TypeName));

	private static string FieldNames(ComponentSnapshot component) =>
		component.Fields.Count == 0
			? "no simple-typed field"
			: string.Join(", ", component.Fields.Select(field => $"{KindLabel(field.Kind)} {field.Name}"));

	private static string KindLabel(int kind) => kind switch
	{
		SaveableFieldKind.Float => "a float",
		SaveableFieldKind.Int => "an int",
		SaveableFieldKind.Bool => "a yes/no value",
		SaveableFieldKind.String => "a string",
		SaveableFieldKind.StringList => "a list of strings",
		SaveableFieldKind.Enum => "an enum's underlying int",
		_ => "a supported value",
	};

	private static string Number(float value) => value.ToString("0.###", CultureInfo.InvariantCulture);

	private static bool IsStateKey(string key) =>
		key is ConditionKey or FavouritedKey or LiquidsKey or ComponentsKey;

	private sealed record Snapshot(
		float Condition,
		bool Favourited,
		IReadOnlyList<LiquidSnapshot> Liquids,
		IReadOnlyList<ComponentSnapshot> Components);

	private sealed record LiquidSnapshot(string LiquidId, float Amount);

	private sealed record ComponentSnapshot(string TypeName, IReadOnlyList<FieldSnapshot> Fields);

	private sealed record FieldSnapshot(
		string Name,
		int Kind,
		float FloatValue,
		int IntValue,
		bool BoolValue,
		string StringValue,
		IReadOnlyList<string> StringList);
}
