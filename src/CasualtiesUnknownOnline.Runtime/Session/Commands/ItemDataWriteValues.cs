using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using CasualtiesUnknownOnline.Runtime.Session.Items;

namespace CasualtiesUnknownOnline.Runtime.Session.Commands;

/// <summary>
/// The value half of a write: whether the value the operator asked for is legal
/// against the item's OWN captured state, and the application of it to that
/// capture. Pure — the command owns the session, the port owns the scene, this owns
/// the arithmetic — so every refusal is pinned by unit tests instead of by a live
/// console.
///
/// <para>
/// The capture is the authority for what the value must be: a component field is
/// written as the kind the item's own field carries (<c>SaveableFieldKind</c>, the
/// same table the capture classifies with), a liquid is written by the id the
/// capture lists, and a property the item does not have is refused by name rather
/// than created. The two texts the answer shows are the renderer's.
/// </para>
/// </summary>
internal static class ItemDataWriteValues
{
	/// <summary>
	/// Applies one parsed write to <paramref name="item"/> (the local capture, which
	/// the caller hands on as the desired state once this answers true), reporting
	/// the value before and after for the answer line.
	/// </summary>
	internal static bool TryApply(CharacterItemMsg item, ItemDataWrite write, out string before, out string after, out string? error)
	{
		before = "";
		after = "";
		error = null;

		return write.Property switch
		{
			ItemDataProperty.Condition => ApplyCondition(item, write, out before, out after, out error),
			ItemDataProperty.Favourited => ApplyFavourited(item, write, out before, out after, out error),
			ItemDataProperty.Liquid => ApplyLiquid(item, write, out before, out after, out error),
			_ => ApplyComponent(item, write, out before, out after, out error),
		};
	}

	/// <summary>
	/// A condition is the game's own scalar and the game destroys what reaches zero
	/// on its own terms, so the verb refuses only what the data model cannot hold: a
	/// negative condition is not a state the game ever produces.
	/// </summary>
	private static bool ApplyCondition(CharacterItemMsg item, ItemDataWrite write, out string before, out string after, out string? error)
	{
		before = ItemDataRenderer.NumberValue(item.Condition);
		after = "";
		if (write.Number < 0f)
		{
			error = $"A condition is 0 or more — got {ItemDataRenderer.NumberValue(write.Number)}.";
			return false;
		}

		item.Condition = write.Number;
		after = ItemDataRenderer.NumberValue(item.Condition);
		error = null;
		return true;
	}

	private static bool ApplyFavourited(CharacterItemMsg item, ItemDataWrite write, out string before, out string after, out string? error)
	{
		before = ItemDataRenderer.YesNoValue(item.Favourited);
		item.Favourited = write.Flag;
		after = ItemDataRenderer.YesNoValue(item.Favourited);
		error = null;
		return true;
	}

	private static bool ApplyLiquid(CharacterItemMsg item, ItemDataWrite write, out string before, out string after, out string? error)
	{
		before = "";
		after = "";
		if (write.Number < 0f)
		{
			error = $"A liquid amount is 0 or more — got {ItemDataRenderer.NumberValue(write.Number)} ml.";
			return false;
		}

		var stack = item.Liquids.FirstOrDefault(liquid => string.Equals(liquid.LiquidId, write.Name, StringComparison.Ordinal));
		if (stack is null)
		{
			// The capture cannot tell "no liquid container at all" from "a container
			// that is empty" (both capture as no stacks), so the refusal names both
			// readings instead of picking one.
			error = $"#{item.InstanceId} carries no '{write.Name}' — its liquids are: {Liquids(item)} (an item with no liquid container reads the same way).";
			return false;
		}

		before = $"{ItemDataRenderer.NumberValue(stack.Amount)} ml";
		stack.Amount = write.Number;
		after = $"{ItemDataRenderer.NumberValue(stack.Amount)} ml";
		error = null;
		return true;
	}

	private static bool ApplyComponent(CharacterItemMsg item, ItemDataWrite write, out string before, out string after, out string? error)
	{
		before = "";
		after = "";
		var component = item.Components.FirstOrDefault(state => string.Equals(state.TypeName, write.Component, StringComparison.Ordinal));
		if (component is null)
		{
			error = $"#{item.InstanceId} has no component '{write.Component}' — it carries: {Components(item)}.";
			return false;
		}

		var field = component.Fields.FirstOrDefault(candidate => string.Equals(candidate.Name, write.Name, StringComparison.Ordinal));
		if (field is null)
		{
			error = $"Component '{write.Component}' has no field '{write.Name}' — it carries: {Fields(component)}.";
			return false;
		}

		before = ItemDataRenderer.FieldValue(field);
		if (!TryWriteField(field, write.Text))
		{
			error = $"'{write.Text}' is not {KindLabel(field.Kind)} for {write.Component}.{write.Name}.";
			before = "";
			return false;
		}

		after = ItemDataRenderer.FieldValue(field);
		error = null;
		return true;
	}

	/// <summary>
	/// The value as the field's own kind holds it. A kind the capture never stores
	/// (an unsupported field never reaches the wire) refuses rather than guesses —
	/// the same closed set <c>ItemStateCodec</c> writes back with.
	/// </summary>
	private static bool TryWriteField(ComponentFieldMsg field, string text)
	{
		switch (field.Kind)
		{
			case SaveableFieldKind.Float when TryParseNumber(text, out var number):
				field.FloatValue = number;
				return true;
			case SaveableFieldKind.Int when int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var integer):
				field.IntValue = integer;
				return true;
			// An enum crosses as its underlying int (ComponentFieldMsg.Kind 6), so that
			// number IS the value the game boxes back onto the field.
			case SaveableFieldKind.Enum when int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var underlying):
				field.IntValue = underlying;
				return true;
			case SaveableFieldKind.Bool when TryParseBool(text, out var flag):
				field.BoolValue = flag;
				return true;
			case SaveableFieldKind.String:
				field.StringValue = text;
				return true;
			case SaveableFieldKind.StringList:
				field.StringList = SplitList(text);
				return true;
			default:
				return false;
		}
	}

	/// <summary>A float field takes the same invariant, finite number the command line's own numbers do.</summary>
	private static bool TryParseNumber(string text, out float number) =>
		float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out number)
		&& !float.IsNaN(number)
		&& !float.IsInfinity(number);

	/// <summary>A list is written as the read half prints it: comma-separated, and an empty value is the empty list.</summary>
	private static List<string> SplitList(string text) =>
		text.Length == 0
			? []
			: [.. text.Split(',').Select(entry => entry.Trim()).Where(entry => entry.Length > 0)];

	private static bool TryParseBool(string text, out bool flag)
	{
		if (string.Equals(text, "true", StringComparison.OrdinalIgnoreCase) || string.Equals(text, "yes", StringComparison.OrdinalIgnoreCase))
		{
			flag = true;
			return true;
		}

		if (string.Equals(text, "false", StringComparison.OrdinalIgnoreCase) || string.Equals(text, "no", StringComparison.OrdinalIgnoreCase))
		{
			flag = false;
			return true;
		}

		flag = false;
		return false;
	}

	private static string KindLabel(int kind) => kind switch
	{
		SaveableFieldKind.Float => "a float",
		SaveableFieldKind.Int => "an int",
		SaveableFieldKind.Bool => "a yes/no",
		SaveableFieldKind.String => "a string",
		SaveableFieldKind.StringList => "a comma-separated string list",
		SaveableFieldKind.Enum => "an enum's underlying int",
		_ => "a supported",
	};

	/// <summary>The bare kind name, for a list of what a component carries.</summary>
	private static string KindName(int kind) => kind switch
	{
		SaveableFieldKind.Float => "float",
		SaveableFieldKind.Int => "int",
		SaveableFieldKind.Bool => "bool",
		SaveableFieldKind.String => "string",
		SaveableFieldKind.StringList => "strings",
		SaveableFieldKind.Enum => "enum",
		_ => "?",
	};

	private static string Liquids(CharacterItemMsg item) =>
		item.Liquids.Count == 0
			? "none"
			: string.Join(", ", item.Liquids.Select(liquid => $"{liquid.LiquidId} {ItemDataRenderer.NumberValue(liquid.Amount)} ml"));

	private static string Components(CharacterItemMsg item) =>
		item.Components.Count == 0
			? "no captured component"
			: string.Join(", ", item.Components.Select(component => component.TypeName));

	private static string Fields(ComponentStateMsg component) =>
		component.Fields.Count == 0
			? "no simple-typed field"
			: string.Join(", ", component.Fields.Select(field => $"{KindName(field.Kind)} {field.Name}"));
}
