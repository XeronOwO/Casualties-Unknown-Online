using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using CasualtiesUnknownOnline.GameState.Domains.Items;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using CasualtiesUnknownOnline.Runtime.Session.Items;

namespace CasualtiesUnknownOnline.Runtime.Session.Commands;

/// <summary>
/// The item-data verb's text form: one line per item when a body is listed, one
/// property per line when a single item is read. Pure — the command handler owns
/// the session and the kernel, this owns what the player reads — so the wording
/// of every property, and which properties are printed at all, is pinned by unit
/// tests instead of by a live console.
///
/// Which properties exist is the kernel's own payload
/// (<see cref="ItemData"/>): the SavedItem fields, the liquid stacks and the
/// typed component fields. Values are rendered as their kind, because the kind is
/// what a later write has to name.
/// </summary>
internal static class ItemDataRenderer
{
	/// <summary>One line per item a body holds, in slot order.</summary>
	internal static string List(ulong owner, IReadOnlyList<ItemState> items, IReadOnlyList<ItemState> all)
	{
		var builder = new StringBuilder();
		builder.Append("Items recorded for ").Append(owner).Append(" (").Append(items.Count).Append("):");
		foreach (var item in items)
		{
			builder.Append('\n')
				.Append('#').Append(item.Identity.InstanceId)
				.Append(' ').Append(item.Identity.DefinitionId)
				.Append(" [").Append(Slot(item)).Append(']')
				.Append(" condition ").Append(Number(item.Data.Condition));

			var contents = Children(item, all).Count;
			if (contents > 0)
			{
				builder.Append(", ").Append(contents).Append(" content(s)");
			}
		}

		return builder.ToString();
	}

	/// <summary>Every property of one item, its direct contents by id (a nested item is read by its own id).</summary>
	internal static string One(ItemState item, IReadOnlyList<ItemState> all)
	{
		var builder = new StringBuilder();
		builder.Append('#').Append(item.Identity.InstanceId)
			.Append(' ').Append(item.Identity.DefinitionId)
			.Append(" [").Append(LocationOf(item)).Append(']')
			.Append(" revision ").Append(item.Revision)
			.Append("\n  condition ").Append(Number(item.Data.Condition))
			.Append("\n  favourited ").Append(item.Data.Favourited ? "yes" : "no");

		foreach (var liquid in item.Data.Liquids)
		{
			builder.Append("\n  liquid ").Append(liquid.LiquidId).Append(' ').Append(Number(liquid.Amount)).Append(" ml");
		}

		foreach (var component in item.Data.Components)
		{
			builder.Append("\n  component ").Append(component.TypeName).Append(": ").Append(Fields(component));
		}

		var children = Children(item, all);
		if (children.Count > 0)
		{
			builder.Append("\n  contents (").Append(children.Count).Append("):");
			foreach (var child in children)
			{
				builder.Append(" #").Append(child.Identity.InstanceId).Append(' ').Append(child.Identity.DefinitionId);
			}
		}

		return builder.ToString();
	}

	/// <summary>The items the kernel records INSIDE this one, by the parent linkage.</summary>
	private static List<ItemState> Children(ItemState item, IReadOnlyList<ItemState> all)
	{
		var children = new List<ItemState>();
		foreach (var row in all)
		{
			if (row.Location.Kind == ItemLocationKind.Contained
				&& row.Location.ParentItemId == item.Identity.InstanceId)
			{
				children.Add(row);
			}
		}

		return children;
	}

	/// <summary>The short form for a listing: which slot or limb of its owner the item sits in.</summary>
	private static string Slot(ItemState item) => item.Location.Kind switch
	{
		ItemLocationKind.Carried when item.Data.SlotIndex >= 0 => $"slot {item.Data.SlotIndex}",
		ItemLocationKind.Carried => "carried",
		ItemLocationKind.Applied => $"limb {item.Location.LimbIndex}",
		_ => LocationOf(item),
	};

	/// <summary>The full form for one item: where it is, and whose it is when somebody owns it.</summary>
	internal static string LocationOf(ItemState item) => item.Location.Kind switch
	{
		ItemLocationKind.Carried when item.Data.SlotIndex >= 0 =>
			$"carried by {item.Location.Owner.Value}, slot {item.Data.SlotIndex}",
		ItemLocationKind.Carried => $"carried by {item.Location.Owner.Value}",
		ItemLocationKind.Contained => $"inside #{item.Location.ParentItemId}",
		ItemLocationKind.Applied => $"applied to {item.Location.Owner.Value}, limb {item.Location.LimbIndex}",
		ItemLocationKind.World => $"world ({Number(item.Location.X)}, {Number(item.Location.Y)})",
		ItemLocationKind.Terminal => "terminal (destroyed or consumed)",
		_ => item.Location.Kind.ToString(),
	};

	private static string Fields(ItemComponentState component)
	{
		if (component.Fields.Count == 0)
		{
			return "(no simple-typed field)";
		}

		var builder = new StringBuilder();
		foreach (var field in component.Fields)
		{
			if (builder.Length > 0)
			{
				builder.Append(", ");
			}

			builder.Append(Kind(field.Kind)).Append(' ').Append(field.Name).Append('=').Append(Value(field));
		}

		return builder.ToString();
	}

	private static string Kind(ItemComponentFieldKind kind) => kind switch
	{
		ItemComponentFieldKind.Float => "float",
		ItemComponentFieldKind.Int => "int",
		ItemComponentFieldKind.Bool => "bool",
		ItemComponentFieldKind.String => "string",
		ItemComponentFieldKind.StringList => "strings",
		ItemComponentFieldKind.Enum => "enum",
		_ => "?",
	};

	private static string Value(ItemComponentField field) => field.Kind switch
	{
		ItemComponentFieldKind.Float => Number(field.FloatValue),
		ItemComponentFieldKind.Int or ItemComponentFieldKind.Enum => field.IntValue.ToString(CultureInfo.InvariantCulture),
		ItemComponentFieldKind.Bool => field.BoolValue ? "true" : "false",
		ItemComponentFieldKind.String => '"' + field.StringValue + '"',
		ItemComponentFieldKind.StringList => "[" + string.Join(", ", field.StringList) + "]",
		_ => "?",
	};

	/// <summary>A number, in the form every line of this verb prints one.</summary>
	internal static string NumberValue(float value) => Number(value);

	/// <summary>
	/// One write COMMAND's answer when it changed more than one property — the same
	/// line as <see cref="Wrote"/>, with one "property before -> after" clause per
	/// property in the order the operator named them. Kept beside the single-property
	/// pair rather than replacing them so the verbs an operator already uses print
	/// exactly what they printed before, and so a merge or a remove cannot silently
	/// reword the answer of a verb that did not change.
	/// </summary>
	internal static string WroteMany(CharacterItemMsg item, IReadOnlyList<ItemDataWrite> writes, IReadOnlyList<string> changes, bool awaitingHost)
	{
		var builder = new StringBuilder();
		builder.Append('#').Append(item.InstanceId).Append(' ').Append(item.ItemId).Append(": ");
		for (var i = 0; i < writes.Count; i++)
		{
			if (i > 0)
			{
				builder.Append("; ");
			}

			builder.Append(PropertyName(writes[i])).Append(' ').Append(changes[i]);
		}

		builder.Append(awaitingHost
			? " — written here and reported to the host, which judges a member's write by its own rule; a refusal puts the old value back and says so in this console."
			: " — written and reported as this client's own item state.");

		// The same one consequence the single-property answer carries: an item whose
		// condition reaches zero may be destroyed by its own rule a moment later.
		if (writes.Any(write => !write.Clear && write.Property == ItemDataProperty.Condition && write.Number == 0f))
		{
			builder.Append(" At 0 the item's own rule may destroy it (destroyAtZeroCondition), so it can be gone a moment from now.");
		}

		return builder.ToString();
	}

	/// <summary>The item's own favourite flag in this verb's words for it (the read half's "yes"/"no").</summary>
	internal static string YesNoValue(bool value) => value ? "yes" : "no";

	/// <summary>
	/// One CAPTURED component field's value, by the kind the capture tagged it with
	/// (<c>SaveableFieldKind</c>): the write half reads and writes the local capture's
	/// own shape, so its values are formatted here beside the kernel row's.
	/// </summary>
	internal static string FieldValue(ComponentFieldMsg field) => Captured(field);

	/// <summary>
	/// A write's property as the answer names it — the one wording the write verbs
	/// and the <c>give</c> verb share, so an operator reads the same word whichever
	/// verb set the value.
	/// </summary>
	internal static string PropertyName(ItemDataWrite write) => write.Property switch
	{
		ItemDataProperty.Condition => "condition",
		ItemDataProperty.Favourited => "favourited",
		ItemDataProperty.Liquid => $"liquid {write.Name}",
		_ => $"component {write.Component} {write.Name}",
	};

	private static string Captured(ComponentFieldMsg field) => field.Kind switch
	{
		SaveableFieldKind.Float => Number(field.FloatValue),
		SaveableFieldKind.Int or SaveableFieldKind.Enum => field.IntValue.ToString(CultureInfo.InvariantCulture),
		SaveableFieldKind.Bool => field.BoolValue ? "true" : "false",
		SaveableFieldKind.String => '"' + field.StringValue + '"',
		SaveableFieldKind.StringList => "[" + string.Join(", ", field.StringList) + "]",
		_ => "?",
	};

	private static string Number(float value) => value.ToString("0.###", CultureInfo.InvariantCulture);
}
