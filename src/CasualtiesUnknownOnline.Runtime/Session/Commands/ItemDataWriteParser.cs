using System;
using System.Collections.Generic;
using System.Globalization;

namespace CasualtiesUnknownOnline.Runtime.Session.Commands;

/// <summary>
/// The item-data write grammar, as tokens after the verb: one of the read half's
/// target shapes followed by what to change —
/// <c>condition &lt;n&gt;</c>, <c>favourited &lt;bool&gt;</c>,
/// <c>liquid &lt;liquidId&gt; &lt;ml&gt;</c> or
/// <c>component &lt;TypeName&gt; &lt;field&gt; &lt;value&gt;</c>.
///
/// <para>
/// The target is parsed by <see cref="ItemDataQueryParser"/> — the SAME grammar the
/// read verb uses — so every verb admits one selector vocabulary, and the property is
/// a closed keyword set: an unknown word is refused with the usage line rather than
/// guessed at, so a typo cannot write a property the operator did not name. The
/// property NAMES are the ones the read half prints, so an operator sets what the
/// verb just showed.
/// </para>
///
/// <para>
/// The three verbs differ only in how many properties they take and whether a value
/// follows: <c>set</c> takes exactly one with a value, <c>merge</c> takes one or more
/// with values, and <c>remove</c> takes one or more WITHOUT one — the property goes
/// back to having no value, which is why the third parser refuses the value tokens
/// rather than ignoring them. All three share the target parse and the property
/// parse, so a property word, its value shape and its refusal wording exist once.
/// </para>
/// </summary>
internal static class ItemDataWriteParser
{
	internal const string Usage = "/data set [<selector>] slot <n> | cursor | id <n> — condition <n> | favourited <bool> | liquid <liquidId> <ml> | component <TypeName> <field> <value>";

	internal const string MergeUsage = "/data merge [<selector>] slot <n> | cursor | id <n> — one or more of: condition <n> | favourited <bool> | liquid <liquidId> <ml> | component <TypeName> <field> <value>";

	internal const string RemoveUsage = "/data remove [<selector>] slot <n> | cursor | id <n> — one or more of: liquid <liquidId> | component <TypeName> <field>";

	private const string ConditionKeyword = "condition";

	private const string FavouritedKeyword = "favourited";

	private const string LiquidKeyword = "liquid";

	private const string ComponentKeyword = "component";

	/// <summary>Parses the tokens that follow the <c>set</c> verb, or reports why they are not a write.</summary>
	internal static bool TryParse(IReadOnlyList<string> tokens, out ItemDataWrite write, out string? error)
	{
		write = Empty();
		if (!ItemDataQueryParser.TryParseTarget(tokens, 0, out var target, out var next, out error))
		{
			return false;
		}

		if (next >= tokens.Count)
		{
			return Refuse($"Usage: {Usage}", out error);
		}

		if (!TryParseProperty(tokens, next, target, clear: false, exact: true, usage: Usage, out var property, out _, out error))
		{
			return false;
		}

		write = property;
		return true;
	}

	/// <summary>
	/// Parses the tokens that follow the <c>merge</c> verb: ONE target, then one or
	/// more property/value pairs. The target is named once because every property in
	/// the command edits the same item — naming it per property would let one command
	/// edit two items, which is the one-item rule the write half already carries.
	/// </summary>
	internal static bool TryParseMany(IReadOnlyList<string> tokens, out IReadOnlyList<ItemDataWrite> writes, out string? error)
	{
		writes = [];
		if (!ItemDataQueryParser.TryParseTarget(tokens, 0, out var target, out var next, out error))
		{
			return false;
		}

		if (next >= tokens.Count)
		{
			return Refuse($"Usage: {MergeUsage}", out error);
		}

		return TryParseProperties(tokens, next, target, MergeUsage, out writes, out error);
	}

	/// <summary>
	/// The property half on its own: one or more property/value pairs starting at
	/// <paramref name="start"/>. The <c>give</c> verb reuses it verbatim — its
	/// destination body and item definition precede the same property list — so the
	/// property words, their value shapes and every refusal exist once in the tree.
	/// <paramref name="target"/> is the item the properties belong to for a write; the
	/// give verb passes the DESTINATION BODY it was typed against, because one record
	/// serves both verbs and the value step never reads the field.
	/// </summary>
	internal static bool TryParseProperties(IReadOnlyList<string> tokens, int start, ItemDataQuery target, string usage, out IReadOnlyList<ItemDataWrite> writes, out string? error)
	{
		writes = [];
		error = null;
		var parsed = new List<ItemDataWrite>();
		var next = start;
		while (next < tokens.Count)
		{
			if (!TryParseProperty(tokens, next, target, clear: false, exact: false, usage: usage, out var property, out next, out error))
			{
				return false;
			}

			parsed.Add(property);
		}

		writes = parsed;
		return true;
	}

	/// <summary>
	/// Parses the tokens that follow the <c>remove</c> verb: one target, then one or
	/// more properties WITHOUT a value. A value token after one is refused rather than
	/// ignored, so <c>/data remove … condition 0</c> is answered instead of quietly
	/// doing something the operator did not ask for.
	/// </summary>
	internal static bool TryParseRemove(IReadOnlyList<string> tokens, out IReadOnlyList<ItemDataWrite> writes, out string? error)
	{
		writes = [];
		if (!ItemDataQueryParser.TryParseTarget(tokens, 0, out var target, out var next, out error))
		{
			return false;
		}

		if (next >= tokens.Count)
		{
			return Refuse($"Usage: {RemoveUsage}", out error);
		}

		var parsed = new List<ItemDataWrite>();
		while (next < tokens.Count)
		{
			if (!TryParsePropertyWithoutValue(tokens, next, target, out var property, out next, out error))
			{
				return false;
			}

			parsed.Add(property);
		}

		writes = parsed;
		return true;
	}

	/// <summary>
	/// One property and its value, starting at <paramref name="next"/>. Returns the
	/// index after what it consumed, which is what makes the merge loop possible
	/// without a second copy of this grammar.
	/// </summary>
	private static bool TryParseProperty(IReadOnlyList<string> tokens, int next, ItemDataQuery target, bool clear, bool exact, string usage, out ItemDataWrite write, out int consumed, out string? error)
	{
		write = Empty();
		consumed = next;
		error = null;
		var property = tokens[next];
		var rest = tokens.Count - next - 1;
		if (IsKeyword(property, ConditionKeyword))
		{
			if (!Enough(rest, 1, exact) || !TryParseNumber(tokens[next + 1], out var condition))
			{
				return RefuseNumber(tokens, next, rest, usage, out error);
			}

			write = new ItemDataWrite(target, ItemDataProperty.Condition, condition, false, "", "", "", clear);
			consumed = next + 2;
			return true;
		}

		if (IsKeyword(property, FavouritedKeyword))
		{
			if (!Enough(rest, 1, exact) || !TryParseFlag(tokens[next + 1], out var favourited))
			{
				return RefuseFlag(tokens, next, rest, usage, out error);
			}

			write = new ItemDataWrite(target, ItemDataProperty.Favourited, 0f, favourited, "", "", "", clear);
			consumed = next + 2;
			return true;
		}

		if (IsKeyword(property, LiquidKeyword))
		{
			if (!Enough(rest, 2, exact) || !TryParseNumber(tokens[next + 2], out var millilitres))
			{
				return RefuseNumber(tokens, next, rest, usage, out error);
			}

			write = new ItemDataWrite(target, ItemDataProperty.Liquid, millilitres, false, tokens[next + 1], "", "", clear);
			consumed = next + 3;
			return true;
		}

		if (IsKeyword(property, ComponentKeyword))
		{
			if (!Enough(rest, 3, exact))
			{
				return Refuse($"Usage: {usage}", out error);
			}

			write = new ItemDataWrite(target, ItemDataProperty.Component, 0f, false, tokens[next + 2], tokens[next + 1], tokens[next + 3], clear);
			consumed = next + 4;
			return true;
		}

		return Refuse($"'{property}' is not a property to write — write condition, favourited, liquid or component. Usage: {usage}", out error);
	}

	/// <summary>
	/// One property WITHOUT a value, for the remove verb: only the two properties that
	/// have an absent state can be named, and anything else is refused here rather
	/// than reaching an apply step that would have to invent a meaning for it.
	/// </summary>
	private static bool TryParsePropertyWithoutValue(IReadOnlyList<string> tokens, int next, ItemDataQuery target, out ItemDataWrite write, out int consumed, out string? error)
	{
		write = Empty();
		consumed = next;
		error = null;
		var property = tokens[next];
		var rest = tokens.Count - next - 1;
		if (IsKeyword(property, LiquidKeyword))
		{
			if (rest < 1)
			{
				return Refuse($"remove {LiquidKeyword} needs the liquid's id. Usage: {RemoveUsage}", out error);
			}

			write = new ItemDataWrite(target, ItemDataProperty.Liquid, 0f, false, tokens[next + 1], "", "", true);
			consumed = next + 2;
			return true;
		}

		if (IsKeyword(property, ComponentKeyword))
		{
			if (rest < 2)
			{
				return Refuse($"remove {ComponentKeyword} needs the component's type name and the field's name. Usage: {RemoveUsage}", out error);
			}

			write = new ItemDataWrite(target, ItemDataProperty.Component, 0f, false, tokens[next + 2], tokens[next + 1], "", true);
			consumed = next + 3;
			return true;
		}

		if (IsKeyword(property, ConditionKeyword) || IsKeyword(property, FavouritedKeyword))
		{
			return Refuse($"'{property}' has no absent state to remove — it is a value with a range, so give it one with the set verb. /data remove accepts liquid and component. Usage: {RemoveUsage}", out error);
		}

		return Refuse($"'{property}' is not a property to remove — remove liquid or component. Usage: {RemoveUsage}", out error);
	}

	private static ItemDataWrite Empty() =>
		new(new ItemDataQuery(ItemDataQueryKind.Bodies, null, -1, 0), ItemDataProperty.Condition, 0f, false, "", "", "");

	/// <summary>
	/// A number is an invariant-culture float, and it must be FINITE: the parser
	/// accepts the words "NaN" and "Infinity", and a condition or a millilitre
	/// count that is neither would travel into the record as poison.
	/// </summary>
	private static bool TryParseNumber(string text, out float number) =>
		float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out number)
		&& !float.IsNaN(number)
		&& !float.IsInfinity(number);

	/// <summary>What the operator reads for a flag is what they may write back: the read half prints "yes"/"no", a component field prints "true"/"false".</summary>
	private static bool TryParseFlag(string text, out bool flag)
	{
		if (string.Equals(text, "true", StringComparison.OrdinalIgnoreCase)
			|| string.Equals(text, "yes", StringComparison.OrdinalIgnoreCase)
			|| string.Equals(text, "on", StringComparison.OrdinalIgnoreCase))
		{
			flag = true;
			return true;
		}

		if (string.Equals(text, "false", StringComparison.OrdinalIgnoreCase)
			|| string.Equals(text, "no", StringComparison.OrdinalIgnoreCase)
			|| string.Equals(text, "off", StringComparison.OrdinalIgnoreCase))
		{
			flag = false;
			return true;
		}

		flag = false;
		return false;
	}

	/// <summary>
	/// The refusal for a property whose value does not parse. It quotes the grammar of the VERB that asked,
	/// because the property vocabulary is shared: a merge that answered with the set usage would tell the operator
	/// to write a shape the merge verb does not accept.
	/// </summary>
	private static bool RefuseNumber(IReadOnlyList<string> tokens, int next, int rest, string usage, out string? error)
	{
		if (rest >= 1 && !TryParseNumber(tokens[next + 1], out _))
		{
			return Refuse($"'{tokens[next + 1]}' is not a number. Usage: {usage}", out error);
		}

		if (rest >= 2 && !TryParseNumber(tokens[next + 2], out _))
		{
			return Refuse($"'{tokens[next + 2]}' is not a number of millilitres. Usage: {usage}", out error);
		}

		return Refuse($"Usage: {usage}", out error);
	}

	private static bool RefuseFlag(IReadOnlyList<string> tokens, int next, int rest, string usage, out string? error) =>
		rest >= 1
			? Refuse($"'{tokens[next + 1]}' is not a yes/no value — write true, false, yes or no. Usage: {usage}", out error)
			: Refuse($"Usage: {usage}", out error);

	/// <summary>
	/// Whether enough tokens are left for this property. The set verb is EXACT — a
	/// token after its one value is refused rather than ignored, so a mistyped extra
	/// word cannot be silently dropped — while merge continues at the next property,
	/// so the same parse is asked whether there are AT LEAST as many as it needs.
	/// </summary>
	private static bool Enough(int rest, int needed, bool exact) =>
		exact ? rest == needed : rest >= needed;

	private static bool IsKeyword(string token, string keyword) =>
		string.Equals(token, keyword, StringComparison.OrdinalIgnoreCase);

	private static bool Refuse(string message, out string? error)
	{
		error = message;
		return false;
	}
}
